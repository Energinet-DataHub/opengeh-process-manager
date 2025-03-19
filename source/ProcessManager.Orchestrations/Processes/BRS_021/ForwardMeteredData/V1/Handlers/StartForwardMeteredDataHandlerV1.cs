// Copyright 2020 Energinet DataHub A/S
//
// Licensed under the Apache License, Version 2.0 (the "License2");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Mapper;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Mapper;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Text;
using GridAreaCode = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.GridAreaCode;
using MeteringPointMasterData = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointMasterData;
using MeteringPointType = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.MeteringPointType;
using OrchestrationInstanceLifecycleState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceLifecycleState;
using OrchestrationStepTerminationState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationStepTerminationState;
using Resolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;
using StepInstanceLifecycleState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.StepInstanceLifecycleState;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class StartForwardMeteredDataHandlerV1(
    ILogger<StartForwardMeteredDataHandlerV1> logger,
    IStartOrchestrationInstanceMessageCommands commands,
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock,
    IMeasurementsMeteredDataClient measurementsMeteredDataClient,
    BusinessValidator<ForwardMeteredDataBusinessValidatedDto> validator,
    ElectricityMarket.Integration.IElectricityMarketViews electricityMarketViews,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
        : StartOrchestrationInstanceFromMessageHandlerBase<ForwardMeteredDataInputV1>(logger)
{
    private readonly IStartOrchestrationInstanceMessageCommands _commands = commands;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;
    private readonly IMeasurementsMeteredDataClient _measurementsMeteredDataClient = measurementsMeteredDataClient;
    private readonly BusinessValidator<ForwardMeteredDataBusinessValidatedDto> _validator = validator;
    private readonly ElectricityMarket.Integration.IElectricityMarketViews _electricityMarketViews = electricityMarketViews;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly ILogger<StartForwardMeteredDataHandlerV1> _logger = logger;

    /// <summary>
    /// This method has multiple commits to the database, to immediately transition lifecycles. This means that
    /// we must implement custom logic to ensure idempotency in case of retries and/or the same message
    /// being received more than once.
    /// </summary>
    protected override async Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        ForwardMeteredDataInputV1 input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
    {
        // Creates an orchestration instance if it doesn't already exist. The orchestration instance
        // should either be in the "terminated" or "running" state.
        var orchestrationInstance = await InitializeOrchestrationInstance(
                actorIdentity,
                input,
                idempotencyKey,
                actorMessageId,
                transactionId,
                meteringPointId)
            .ConfigureAwait(false);

        // If the orchestration instance is terminated, do nothing (idempotency/retry check).
        if (orchestrationInstance.Lifecycle.State is OrchestrationInstanceLifecycleState.Terminated)
            return;

        // If we reach this point, the orchestration instance should be running, so this check is just an extra safeguard.
        if (orchestrationInstance.Lifecycle.State is not OrchestrationInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Orchestration instance must be running (Id={orchestrationInstance.Id}, State={orchestrationInstance.Lifecycle.State}).");

        var forwardMeteredDataInput = orchestrationInstance.ParameterValue.AsType<ForwardMeteredDataInputV1>();

        // Perform step: Business validation
        // TODO: Do we need a RowVersion on the step instances, to ensure this code doesn't run in parallel?
        var validationErrors = await PerformBusinessValidation(
                orchestrationInstance: orchestrationInstance,
                input: forwardMeteredDataInput)
            .ConfigureAwait(false);

        var validationSuccess = validationErrors.Count == 0;
        if (validationSuccess)
        {
            // Perform step: Forward to Measurements
            await ForwardToMeasurements(
                    input,
                    orchestrationInstance)
                .ConfigureAwait(false);
        }
        else
        {
            // Skip step: Forward to Measurements
            var forwardStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.ForwardToMeasurementsStep);

            // If the step is already skipped, do nothing (idempotency/retry check).
            if (forwardStep.Lifecycle.TerminationState is not OrchestrationStepTerminationState.Skipped)
            {
                // If the step isn't skipped, it should still be in "pending", else an exception will be thrown.
                await StepHelper.SkipStepAndCommitIfPending(forwardStep, _clock, _progressRepository)
                    .ConfigureAwait(false);
            }

            // Skip step: Find receiver
            var findReceiverStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.FindReceiversStep);

            // If the step is already skipped, do nothing (idempotency/retry check).
            if (findReceiverStep.Lifecycle.TerminationState != OrchestrationStepTerminationState.Skipped)
            {
                // If the step isn't skipped, it should still be in "pending", else an exception will be thrown.
                await StepHelper.SkipStepAndCommitIfPending(findReceiverStep, _clock, _progressRepository)
                    .ConfigureAwait(false);
            }

            await EnqueueRejectedActorMessage(
                    orchestrationInstance,
                    forwardMeteredDataInput,
                    validationErrors)
                .ConfigureAwait(false);
        }

        // The orchestration instance is now in a state where it either:
        // - Waits for a notify response from measurements on the event hub.
        // - Waits for a rejected actor messages enqueued notify response on the service bus.
    }

    /// <summary>
    /// Create an orchestration instance (if it doesn't already exist), and transition it to running.
    /// <remarks>If the orchestration instance already exists, and is already running or terminated, this method does nothing.</remarks>
    /// </summary>
    private async Task<OrchestrationInstance> InitializeOrchestrationInstance(
        ActorIdentity actorIdentity,
        ForwardMeteredDataInputV1 input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
    {
        // Creates an orchestration instance (if it doesn't exist) and transitions it to queued state.
        var orchestrationInstanceId = await _commands.StartNewOrchestrationInstanceAsync(
                actorIdentity,
                OrchestrationDescriptionBuilderV1.UniqueName.MapToDomain(),
                input,
                skipStepsBySequence: [],
                new IdempotencyKey(idempotencyKey),
                new ActorMessageId(actorMessageId),
                new TransactionId(transactionId),
                meteringPointId is not null ? new MeteringPointId(meteringPointId) : null)
            .ConfigureAwait(false);

        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // Do nothing if the state is already running or terminated
        if (orchestrationInstance.Lifecycle.State is OrchestrationInstanceLifecycleState.Running
            or OrchestrationInstanceLifecycleState.Terminated)
        {
            return orchestrationInstance;
        }

        // Transition the orchestration instance to running. This will throw an exception if the
        // orchestration instance is in an invalid state, but the above guards should ensure that is not possible.
        orchestrationInstance.Lifecycle.TransitionToRunning(_clock);
        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        return orchestrationInstance;
    }

    /// <summary>
    /// Perform business validation. If the step has already run, the existing validation errors are returned.
    /// </summary>
    private async Task<IReadOnlyCollection<ValidationError>> PerformBusinessValidation(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataInputV1 input)
    {
        var validationStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.BusinessValidationStep);

        // If the step is already terminated (idempotency/retry check), return the existing validation errors.
        if (validationStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
        {
            if (validationStep.Lifecycle.TerminationState == OrchestrationStepTerminationState.Failed && validationStep.CustomState.IsEmpty)
                throw new InvalidOperationException("Validation step shouldn't be able to fail without any validation errors.");

            // Get existing validation errors if the step is already terminated.
            return validationStep.CustomState.IsEmpty
                ? []
                : validationStep.CustomState.AsType<IReadOnlyCollection<ValidationError>>();
        }

        await StepHelper.StartStepAndCommitIfPending(validationStep, _clock, _progressRepository).ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (validationStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Validation step must be running (Id={validationStep.Id}, State={validationStep.Lifecycle.State}).");

        // Fetch metering point master data and store received data used to find receiver later in the orchestration
        // TODO: Use master data from the orchestration instance custom state instead
        var meteringPointMasterData = await GetMeteringPointMasterData(
                input.MeteringPointId,
                input.StartDateTime,
                input.EndDateTime)
            .ConfigureAwait(false);

        var validationErrors = await _validator.ValidateAsync(
                new ForwardMeteredDataBusinessValidatedDto(
                    Input: input,
                    MeteringPointMasterData: meteringPointMasterData))
            .ConfigureAwait(false);

        var validationSuccess = validationErrors.Count == 0;

        if (!validationSuccess)
            validationStep.CustomState.SetFromInstance(validationErrors);

        var validationStepTerminationState = validationSuccess
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;

        await StepHelper.TerminateStepAndCommit(validationStep, _clock, _progressRepository, validationStepTerminationState).ConfigureAwait(false);

        return validationErrors;
    }

    private async Task ForwardToMeasurements(
        ForwardMeteredDataInputV1 input,
        OrchestrationInstance orchestrationInstance)
    {
        // Start Step: Forward to Measurements
        var forwardToMeasurementsStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.ForwardToMeasurementsStep);

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (forwardToMeasurementsStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
            return;

        await StepHelper.StartStepAndCommitIfPending(forwardToMeasurementsStep, _clock, _progressRepository).ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (forwardToMeasurementsStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Forward to Measurements step must be running (Id={forwardToMeasurementsStep.Id}, State={forwardToMeasurementsStep.Lifecycle.State}).");

        // TODO: How is idempotency handled here / when Measurements receive this message? Do we need an idempotency key?
        await _measurementsMeteredDataClient.SendAsync(
                MapInputToMeasurements(orchestrationInstance.Id, input),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async Task EnqueueRejectedActorMessage(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataInputV1 forwardMeteredDataInput,
        IReadOnlyCollection<ValidationError> validationErrors)
    {
        var enqueueStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep);

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (enqueueStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
            return;

        await StepHelper.StartStepAndCommitIfPending(enqueueStep, _clock, _progressRepository).ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (enqueueStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Enqueue rejected message step must be running (Id={enqueueStep.Id}, State={enqueueStep.Lifecycle.State}).");

        // Ensure always using the same idempotency key
        Guid idempotencyKey;
        if (enqueueStep.CustomState.IsEmpty)
        {
            idempotencyKey = Guid.NewGuid();
            enqueueStep.CustomState.SetFromInstance(new EnqueueActorMessagesStepCustomStateV1(idempotencyKey));
            await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }
        else
        {
            idempotencyKey = enqueueStep.CustomState.AsType<EnqueueActorMessagesStepCustomStateV1>().IdempotencyKey;
        }

        await _enqueueActorMessagesClient.EnqueueAsync(
                OrchestrationDescriptionBuilderV1.UniqueName,
                orchestrationInstance.Id.Value,
                new ActorIdentityDto(
                    ActorNumber.Create(forwardMeteredDataInput.ActorNumber),
                    ActorRole.FromName(forwardMeteredDataInput.ActorRole)),
                idempotencyKey,
                new ForwardMeteredDataRejectedV1(
                    forwardMeteredDataInput.ActorMessageId,
                    forwardMeteredDataInput.TransactionId,
                    ActorNumber.Create(forwardMeteredDataInput.ActorNumber),
                    ActorRole.FromName(forwardMeteredDataInput.ActorRole),
                    BusinessReason.FromName(forwardMeteredDataInput.BusinessReason),
                    validationErrors
                        .Select(e => new ValidationErrorDto(e.Message, e.ErrorCode))
                        .ToList()))
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<MeteringPointMasterData>> GetMeteringPointMasterData(
        string? meteringPointIdentification,
        string startDateTime,
        string? endDateTime)
    {
        if (meteringPointIdentification is null || endDateTime is null)
        {
            return [];
        }

        var id = new MeteringPointIdentification(meteringPointIdentification);
        var parsedStartDateTime = InstantPatternWithOptionalSeconds.Parse(startDateTime);
        var parsedEndDateTime = InstantPatternWithOptionalSeconds.Parse(endDateTime);

        if (!parsedStartDateTime.Success || !parsedEndDateTime.Success)
        {
            return [];
        }

        // Added temporary try-catch to avoid crashing the process if the call to the electricity market fails for actor tests.
        // When this has been tested and verified, the try-catch should be removed.
        try
        {
            var meteringPointMasterData = await _electricityMarketViews
                .GetMeteringPointMasterDataChangesAsync(
                    id,
                    new Interval(parsedStartDateTime.Value, parsedEndDateTime.Value))
                .ConfigureAwait(false);

            return meteringPointMasterData
                .Select(mpt => new MeteringPointMasterData(
                    new MeteringPointId(mpt.Identification.Value),
                    new GridAreaCode(mpt.GridAreaCode.Value),
                    ActorNumber.Create(mpt.GridAccessProvider),
                    MeteringPointMasterDataMapper.ConnectionStateMap.Map(mpt.ConnectionState),
                    MeteringPointMasterDataMapper.MeteringPointTypeMap.Map(mpt.Type),
                    MeteringPointMasterDataMapper.MeteringPointSubTypeMap.Map(mpt.SubType),
                    MeteringPointMasterDataMapper.MeasureUnitMap.Map(mpt.Unit)))
                .ToList();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to get metering point master data for metering point {meteringPointIdentification} in the period {parsedStartDateTime} - {parsedEndDateTime}.");
            return [];
        }
    }

    private MeteredDataForMeteringPoint MapInputToMeasurements(
        OrchestrationInstanceId orchestrationInstanceId,
        ForwardMeteredDataInputV1 input) =>
        new(
            OrchestrationId: orchestrationInstanceId.Value.ToString(),
            MeteringPointId: input.MeteringPointId!,
            TransactionId: input.TransactionId,
            CreatedAt: InstantPattern.ExtendedIso.Parse(input.RegistrationDateTime).Value,
            StartDateTime: InstantPatternWithOptionalSeconds.Parse(input.StartDateTime).Value,
            EndDateTime: InstantPatternWithOptionalSeconds.Parse(input.EndDateTime!).Value,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            Unit: MeasurementUnit.FromName(input.MeasureUnit!),
            Resolution: Resolution.FromName(input.Resolution!),
            Points: input.MeteredData.Select(
                    MapPoints)
                .ToList());

    private Point MapPoints(ForwardMeteredDataInputV1.MeteredDataWithTimestamp eo)
    {
        // TODO: temporary solution until we have business validation rules for quality
        var quality = string.IsNullOrWhiteSpace(eo.QuantityQuality) || eo.QuantityQuality == Quality.Incomplete.Name
            ? Quality.NotAvailable
            : Quality.FromName(eo.QuantityQuality);
        return new Point(
            int.Parse(eo.Position!),
            decimal.Parse(eo.EnergyQuantity!),
            quality);
    }
}
