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
using ActorNumber = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.ActorNumber;
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

    // TODO: This method is not idempotent, Since we can not set a "running" step to "running"
    // TODO: Hence we need to commit after the event/message has been sent
    protected override async Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        ForwardMeteredDataInputV1 input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
    {
        var orchestrationInstance = await InitializeOrchestrationInstance(
                actorIdentity,
                input,
                idempotencyKey,
                actorMessageId,
                transactionId,
                meteringPointId)
            .ConfigureAwait(false);

        // If the orchestration instance isn't running, do nothing (idempotency/retry check).
        if (orchestrationInstance.Lifecycle.State != OrchestrationInstanceLifecycleState.Running)
            return;

        var forwardMeteredDataInput = orchestrationInstance.ParameterValue.AsType<ForwardMeteredDataInputV1>();

        // Perform step: Business validation
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
            var forwardStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.ForwardToMeasurementStep);

            // If the step is already skipped, do nothing (idempotency/retry check).
            if (forwardStep.Lifecycle.TerminationState != OrchestrationStepTerminationState.Skipped)
            {
                // If the step isn't skipped, it should still be in "pending", else an exception will be thrown.
                await StepHelper.SkipStepAndCommitIfPending(forwardStep, _clock, _progressRepository)
                    .ConfigureAwait(false);
            }

            // Skip step: Find receiver
            var findReceiverStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.FindReceiverStep);

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

    private async Task<OrchestrationInstance> InitializeOrchestrationInstance(
        ActorIdentity actorIdentity,
        ForwardMeteredDataInputV1 input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
    {
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

        // Initialize orchestration instance
        if (orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Queued)
        {
            orchestrationInstance.Lifecycle.TransitionToRunning(_clock);
            await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

        return orchestrationInstance;
    }

    private async Task<IReadOnlyCollection<ValidationError>> PerformBusinessValidation(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataInputV1 input)
    {
        var validationStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.BusinessValidationStep);
        await StepHelper.StartStepAndCommitIfPending(validationStep, _clock, _progressRepository).ConfigureAwait(false);

        IReadOnlyCollection<ValidationError> validationErrors;
        if (validationStep.Lifecycle.State == StepInstanceLifecycleState.Running)
        {
            // Fetch Metered Data and store received data used to find receiver later in the orchestration
            // TODO: Use master data from the orchestration instance custom state instead
            var meteringPointMasterDataList = await GetMeteringPointMasterData(
                    input.MeteringPointId,
                    input.StartDateTime,
                    input.EndDateTime)
                .ConfigureAwait(false);

            // Validate Metered Data
            validationErrors = await _validator.ValidateAsync(
                    new ForwardMeteredDataBusinessValidatedDto(
                        Input: input,
                        MeteringPointMasterData: meteringPointMasterDataList))
                .ConfigureAwait(false);

            var validationSuccess = validationErrors.Count == 0;

            if (!validationSuccess)
                validationStep.CustomState.SetFromInstance(validationErrors);

            var validationStepTerminationState = validationSuccess
                ? OrchestrationStepTerminationState.Succeeded
                : OrchestrationStepTerminationState.Failed;

            // Terminate Step: Validate Metered Data
            await StepHelper.TerminateStepAndCommit(validationStep, _clock, _progressRepository, validationStepTerminationState).ConfigureAwait(false);
        }
        else
        {
            if (validationStep.Lifecycle.State is not StepInstanceLifecycleState.Terminated)
                throw new InvalidOperationException($"Validation step is not running or terminated (Step.Id={validationStep.Id}, Step.State={validationStep.Lifecycle.State}).");

            if (validationStep.Lifecycle.TerminationState == OrchestrationStepTerminationState.Failed && validationStep.CustomState.IsEmpty)
                throw new InvalidOperationException("Validation step shouldn't be able to fail without any validation errors.");

            validationErrors = validationStep.CustomState.IsEmpty
                ? []
                : validationStep.CustomState.AsType<IReadOnlyCollection<ValidationError>>();
        }

        return validationErrors;
    }

    private async Task ForwardToMeasurements(
        ForwardMeteredDataInputV1 input,
        OrchestrationInstance orchestrationInstance)
    {
        // Start Step: Forward to Measurements
        var forwardToMeasurementStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.ForwardToMeasurementStep);
        await StepHelper.StartStepAndCommitIfPending(forwardToMeasurementStep, _clock, _progressRepository).ConfigureAwait(false);

        if (forwardToMeasurementStep.Lifecycle.State == StepInstanceLifecycleState.Running)
        {
            await _measurementsMeteredDataClient.SendAsync(
                    MapInputToMeasurements(orchestrationInstance.Id, input),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private async Task EnqueueRejectedActorMessage(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataInputV1 forwardMeteredDataInput,
        IReadOnlyCollection<ValidationError> validationErrors)
    {
        await _enqueueActorMessagesClient.EnqueueAsync(
                OrchestrationDescriptionBuilderV1.UniqueName,
                orchestrationInstance.Id.Value,
                new ActorIdentityDto(
                    ProcessManager.Abstractions.Core.ValueObjects.ActorNumber.Create(
                        forwardMeteredDataInput.ActorNumber),
                    ActorRole.FromName(forwardMeteredDataInput.ActorRole)),
                Guid.NewGuid(),
                new ForwardMeteredDataRejectedV1(
                    forwardMeteredDataInput.ActorMessageId,
                    forwardMeteredDataInput.TransactionId,
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

        var id = new ElectricityMarket.Integration.Models.MasterData.MeteringPointIdentification(meteringPointIdentification);
        var parsedStartDateTime = InstantPatternWithOptionalSeconds.Parse(startDateTime);
        var parsedEndDateTime = InstantPatternWithOptionalSeconds.Parse(endDateTime);

        if (!parsedStartDateTime.Success || !parsedEndDateTime.Success)
        {
            return [];
        }

        var meteringPointMasterData = await _electricityMarketViews
            .GetMeteringPointMasterDataChangesAsync(
                new MeteringPointIdentification(meteringPointIdentification),
                new Interval(parsedStartDateTime.Value, parsedEndDateTime.Value))
            .ConfigureAwait(false);

        return meteringPointMasterData
            .Select(mpt => new MeteringPointMasterData(
                new MeteringPointId(mpt.Identification.Value),
                new GridAreaCode(mpt.GridAreaCode.Value),
                new ActorNumber(mpt.GridAccessProvider),
                MeteringPointMasterDataMapper.ConnectionStateMap.Map(mpt.ConnectionState),
                MeteringPointMasterDataMapper.MeteringPointTypeMap.Map(mpt.Type),
                MeteringPointMasterDataMapper.MeteringPointSubTypeMap.Map(mpt.SubType),
                MeteringPointMasterDataMapper.MeasureUnitMap.Map(mpt.Unit)))
            .ToList();
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
            Points: input.EnergyObservations.Select(
                    eo => new Point(
                        int.Parse(eo.Position!),
                        decimal.Parse(eo.EnergyQuantity!),
                        Quality.FromName(eo.QuantityQuality!)))
                .ToList());
}
