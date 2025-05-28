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

using System.Text.Json;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.FeatureManagement;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using NodaTime;
using OrchestrationInstanceLifecycleState =
    Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceLifecycleState;
using StepInstanceLifecycleState =
    Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.StepInstanceLifecycleState;
using StepInstanceTerminationState =
    Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.StepInstanceTerminationState;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class StartForwardMeteredDataHandlerV1(
    ILogger<StartForwardMeteredDataHandlerV1> logger,
    IStartOrchestrationInstanceMessageCommands commands,
    ISendMeasurementsInstanceRepository sendMeasurementsInstanceRepository,
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock,
    IMeasurementsClient measurementsClient,
    BusinessValidator<ForwardMeteredDataBusinessValidatedDto> validator,
    IMeteringPointMasterDataProvider meteringPointMasterDataProvider,
    IEnqueueActorMessagesClient enqueueActorMessagesClient,
    DelegationProvider delegationProvider,
    TelemetryClient telemetryClient,
    IFeatureManager featureManager)
    : StartOrchestrationInstanceHandlerBase<ForwardMeteredDataInputV1>(logger)
{
    private readonly IStartOrchestrationInstanceMessageCommands _commands = commands;
    private readonly ISendMeasurementsInstanceRepository _sendMeasurementsInstanceRepository = sendMeasurementsInstanceRepository;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;
    private readonly IMeasurementsClient _measurementsClient = measurementsClient;
    private readonly BusinessValidator<ForwardMeteredDataBusinessValidatedDto> _validator = validator;
    private readonly IMeteringPointMasterDataProvider _meteringPointMasterDataProvider = meteringPointMasterDataProvider;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly DelegationProvider _delegationProvider = delegationProvider;
    private readonly TelemetryClient _telemetryClient = telemetryClient;
    private readonly IFeatureManager _featureManager = featureManager;

    public override bool CanHandle(StartOrchestrationInstanceV1 startOrchestrationInstance) =>
        startOrchestrationInstance.OrchestrationVersion == Brs_021_ForwardedMeteredData.V1.Version &&
        startOrchestrationInstance.OrchestrationName == Brs_021_ForwardedMeteredData.V1.Name;

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

        if (await _featureManager.UseNewSendMeasurementsTable().ConfigureAwait(false))
        {
            await InitializeSendMeasurementsInstance(
                    actorIdentity.Actor,
                    input,
                    new IdempotencyKey(idempotencyKey),
                    new TransactionId(transactionId),
                    meteringPointId)
                .ConfigureAwait(false);
        }

        // If the orchestration instance is terminated, do nothing (idempotency/retry check).
        if (orchestrationInstance.Lifecycle.State is OrchestrationInstanceLifecycleState.Terminated)
            return;

        // If we reach this point, the orchestration instance should be running, so this check is just an extra safeguard.
        if (orchestrationInstance.Lifecycle.State is not OrchestrationInstanceLifecycleState.Running)
        {
            throw new InvalidOperationException(
                $"Orchestration instance must be running (Id={orchestrationInstance.Id}, State={orchestrationInstance.Lifecycle.State}).");
        }

        var forwardMeteredDataInput = orchestrationInstance.ParameterValue.AsType<ForwardMeteredDataInputV1>();

        // Fetch metering point master data and store if needed
        if (orchestrationInstance.CustomState.IsEmpty)
        {
            var historicalMeteringPointMasterData =
                await _meteringPointMasterDataProvider
                .GetMasterData(input.MeteringPointId!, input.StartDateTime, input.EndDateTime!)
                .ConfigureAwait(false);

            orchestrationInstance.CustomState.SetFromInstance(
                new ForwardMeteredDataCustomStateV2(
                    HistoricalMeteringPointMasterData: ForwardMeteredDataCustomStateV2.MasterData.FromMeteringPointMasterData(historicalMeteringPointMasterData)));

            await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

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
                    ForwardMeteredDataValidInput.From(input),
                    orchestrationInstance)
                .ConfigureAwait(false);
        }
        else
        {
            // Skip step: Forward to Measurements
            var forwardStep =
                orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.ForwardToMeasurementsStep);

            // If the step is already skipped, do nothing (idempotency/retry check).
            if (forwardStep.Lifecycle.TerminationState is not StepInstanceTerminationState.Skipped)
            {
                // If the step isn't skipped, it should still be in "pending", else an exception will be thrown.
                await StepHelper.SkipStepAndCommitIfPending(forwardStep, _clock, _progressRepository)
                    .ConfigureAwait(false);
            }

            // Skip step: Find receiver
            var findReceiverStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.FindReceiversStep);

            // If the step is already skipped, do nothing (idempotency/retry check).
            if (findReceiverStep.Lifecycle.TerminationState != StepInstanceTerminationState.Skipped)
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

    private static (ActorNumber StartedByActorNumber, ActorRole StartedByActorRole) GetStartedByActor(
        OrchestrationInstance orchestrationInstance)
    {
        var orchestrationStartedBy = orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto();
        return orchestrationStartedBy switch
        {
            ActorIdentityDto actor => (actor.ActorNumber, actor.ActorRole),
            _ => throw new ArgumentOutOfRangeException(
                nameof(orchestrationStartedBy),
                orchestrationStartedBy.GetType().Name,
                "Unknown enqueuedBy type"),
        };
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
                OrchestrationDescriptionBuilder.UniqueName.MapToDomain(),
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
    /// Create a Send Measurements instance (if it doesn't already exist), and returns it.
    /// <remarks>If the Send Measurements instance already exists, the existing instance is returned.</remarks>
    /// </summary>
    private async Task<SendMeasurementsInstance> InitializeSendMeasurementsInstance(
        Actor actor,
        ForwardMeteredDataInputV1 input,
        IdempotencyKey idempotencyKey,
        TransactionId transactionId,
        string? meteringPointId)
    {
        // Creates a Send Measurements instance (if it doesn't already exist).
        var instance = await _sendMeasurementsInstanceRepository.GetOrDefaultAsync(idempotencyKey).ConfigureAwait(false);

        if (instance == null)
        {
            instance = new SendMeasurementsInstance(
                createdAt: _clock.GetCurrentInstant(),
                createdBy: actor,
                transactionId: transactionId,
                meteringPointId: meteringPointId is not null ? new MeteringPointId(meteringPointId) : null,
                idempotencyKey: idempotencyKey);

            using var inputAsStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(inputAsStream, input).ConfigureAwait(false);

            // Must await the AddAsync method to ensure the input is uploaded to the file storage before committing.
            await _sendMeasurementsInstanceRepository.AddAsync(instance, inputAsStream)
                .ConfigureAwait(false);

            await _sendMeasurementsInstanceRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

        return instance;
    }

    /// <summary>
    /// Perform business validation. If the step has already run, the existing validation errors are returned.
    /// </summary>
    private async Task<IReadOnlyCollection<ValidationError>> PerformBusinessValidation(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataInputV1 input)
    {
        var validationStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.BusinessValidationStep);

        // If the step is already terminated (idempotency/retry check), return the existing validation errors.
        if (validationStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
        {
            if (validationStep.Lifecycle.TerminationState == StepInstanceTerminationState.Failed
                && validationStep.CustomState.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Validation step shouldn't be able to fail without any validation errors.");
            }

            // Get existing validation errors if the step is already terminated.
            return validationStep.CustomState.IsEmpty
                ? []
                : validationStep.CustomState.AsType<IReadOnlyCollection<ValidationError>>();
        }

        await StepHelper.StartStepAndCommitIfPending(validationStep, _clock, _progressRepository).ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (validationStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
        {
            throw new InvalidOperationException(
                $"Validation step must be running (Id={validationStep.Id}, State={validationStep.Lifecycle.State}).");
        }

        // Fetch metering point master data and store received data used to find receiver later in the orchestration
        var forwardMeteredDataCustomState = orchestrationInstance.CustomState.AsType<ForwardMeteredDataCustomStateV2>();

        var delegationResult = await IsIncomingMeteredDataDelegated(orchestrationInstance, forwardMeteredDataCustomState)
            .ConfigureAwait(false);
        if (delegationResult is { ShouldBeDelegated: true })
        {
            if (delegationResult is { DelegatedFromActorNumber: null })
            {
                await StepHelper.TerminateStepAndCommit(
                        validationStep,
                        _clock,
                        _progressRepository,
                        _telemetryClient,
                        StepInstanceTerminationState.Failed)
                    .ConfigureAwait(false);
                return new List<ValidationError>()
                {
                    new(
                        "Aktør ikke opsat til at må indsende måledata for det pågældende målepunkt/"
                        + "Actor not allowed to send meterdata for the selected metering point",
                        "D50"),
                };
            }

            input = input with { GridAccessProviderNumber = delegationResult.DelegatedFromActorNumber };
        }

        var validationErrors = await _validator.ValidateAsync(
                new ForwardMeteredDataBusinessValidatedDto(
                    Input: input,
                    MeteringPointMasterData: forwardMeteredDataCustomState.HistoricalMeteringPointMasterData
                        .Select(mpmd => mpmd.ToMeteringPointMasterData())
                        .ToList()))
            .ConfigureAwait(false);

        var validationSuccess = validationErrors.Count == 0;

        if (!validationSuccess)
            validationStep.CustomState.SetFromInstance(validationErrors);

        var validationStepTerminationState = validationSuccess
            ? StepInstanceTerminationState.Succeeded
            : StepInstanceTerminationState.Failed;

        await StepHelper.TerminateStepAndCommit(
                validationStep,
                _clock,
                _progressRepository,
                _telemetryClient,
                validationStepTerminationState)
            .ConfigureAwait(false);

        return validationErrors;
    }

    private async Task<(bool ShouldBeDelegated, string? DelegatedFromActorNumber)> IsIncomingMeteredDataDelegated(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataCustomStateV2 customState)
    {
        if (customState.HistoricalMeteringPointMasterData.Count == 0)
            return (false, null);

        // Grid area owner is and code is always the current metering point master data.
        var currentMeteringPointMasterData = customState
            .HistoricalMeteringPointMasterData.First()
            .ToMeteringPointMasterData();

        var startedByActor = GetStartedByActor(orchestrationInstance);

        return await _delegationProvider.GetDelegatedFromAsync(
            gridAreaOwner: currentMeteringPointMasterData.CurrentGridAccessProvider,
            gridAreaCode: currentMeteringPointMasterData.CurrentGridAreaCode,
            senderActorNumber: startedByActor.StartedByActorNumber,
            senderActorRole: startedByActor.StartedByActorRole).ConfigureAwait(false);
    }

    private async Task ForwardToMeasurements(
        ForwardMeteredDataValidInput input,
        OrchestrationInstance orchestrationInstance)
    {
        // Start Step: Forward to Measurements
        var forwardToMeasurementsStep =
            orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.ForwardToMeasurementsStep);

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (forwardToMeasurementsStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
            return;

        await StepHelper.StartStepAndCommitIfPending(forwardToMeasurementsStep, _clock, _progressRepository)
            .ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (forwardToMeasurementsStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
        {
            throw new InvalidOperationException(
                $"Forward to Measurements step must be running (Id={forwardToMeasurementsStep.Id}, State={forwardToMeasurementsStep.Lifecycle.State}).");
        }

        await _measurementsClient.SendAsync(
                MapInputToMeasurements(orchestrationInstance.Id, input),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async Task EnqueueRejectedActorMessage(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataInputV1 forwardMeteredDataInput,
        IReadOnlyCollection<ValidationError> validationErrors)
    {
        var enqueueStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (enqueueStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
            return;

        await StepHelper.StartStepAndCommitIfPending(enqueueStep, _clock, _progressRepository).ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (enqueueStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
        {
            throw new InvalidOperationException(
                $"Enqueue rejected message step must be running (Id={enqueueStep.Id}, State={enqueueStep.Lifecycle.State}).");
        }

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

        var actorIdentity = ((ActorIdentity)orchestrationInstance.Lifecycle.CreatedBy.Value).Actor;

        await _enqueueActorMessagesClient.EnqueueAsync(
                OrchestrationDescriptionBuilder.UniqueName,
                orchestrationInstance.Id.Value,
                new ActorIdentityDto(
                    ActorNumber.Create(actorIdentity.Number.Value),
                    ActorRole.FromName(actorIdentity.Role.Name)),
                idempotencyKey,
                new ForwardMeteredDataRejectedV1(
                    forwardMeteredDataInput.ActorMessageId,
                    forwardMeteredDataInput.TransactionId,
                    ForwardedForActorRole: ActorRole.FromName(forwardMeteredDataInput.ActorRole),
                    BusinessReason.FromName(forwardMeteredDataInput.BusinessReason),
                    validationErrors
                        .Select(e => new ValidationErrorDto(e.Message, e.ErrorCode))
                        .ToList(),
                    MeteringPointId: forwardMeteredDataInput.MeteringPointId!))
            .ConfigureAwait(false);
    }

    private MeasurementsForMeteringPoint MapInputToMeasurements(
        OrchestrationInstanceId orchestrationInstanceId,
        ForwardMeteredDataValidInput input) =>
        new(
            OrchestrationId: orchestrationInstanceId.Value.ToString(),
            MeteringPointId: input.MeteringPointId.Value,
            TransactionId: input.TransactionId.Value,
            CreatedAt: input.RegistrationDateTime,
            StartDateTime: input.StartDateTime,
            EndDateTime: input.EndDateTime,
            MeteringPointType: input.MeteringPointType,
            Unit: input.MeasureUnit,
            Resolution: input.Resolution,
            Measurements: input.Measurements.Select(
                    MapPoints)
                .ToList());

    private Measurement MapPoints(ForwardMeteredDataValidInput.Measurement measurement)
    {
        return new Measurement(
            measurement.Position,
            // TODO: LRN - Awaiting a final decision from Volt on how to handle null values.
            measurement.EnergyQuantity ?? 0.000m,
            measurement.QuantityQuality);
    }
}
