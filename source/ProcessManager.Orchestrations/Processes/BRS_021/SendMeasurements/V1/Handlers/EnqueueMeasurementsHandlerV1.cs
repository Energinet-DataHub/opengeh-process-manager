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

using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeasurements.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.ApplicationInsights;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Handlers;

public class EnqueueMeasurementsHandlerV1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock,
    IEnqueueActorMessagesClient enqueueActorMessagesClient,
    MeteringPointReceiversProvider meteringPointReceiversProvider,
    TelemetryClient telemetryClient)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly MeteringPointReceiversProvider _meteringPointReceiversProvider = meteringPointReceiversProvider;
    private readonly TelemetryClient _telemetryClient = telemetryClient;

    public async Task HandleAsync(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // If the orchestration instance is terminated, do nothing (idempotency/retry check).
        if (orchestrationInstance.Lifecycle.State is OrchestrationInstanceLifecycleState.Terminated)
            return;

        // If we reach this point, the orchestration instance should be running, so this check is just an extra safeguard.
        if (orchestrationInstance.Lifecycle.State is not OrchestrationInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Orchestration instance must be running (Id={orchestrationInstance.Id}, State={orchestrationInstance.Lifecycle.State}).");

        // only valid data will be enqueued
        var forwardMeasurementsInput = ForwardMeasurementsValidInput.From(orchestrationInstance.ParameterValue.AsType<ForwardMeasurementsInputV1>());

        await TerminateForwardToMeasurementStep(orchestrationInstance).ConfigureAwait(false);

        // Start Step: Find receiver step
        var receiversWithMeasurements = await FindReceivers(orchestrationInstance, forwardMeasurementsInput).ConfigureAwait(false);

        // Start Step: Enqueue actor messages step
        await EnqueueAcceptedActorMessagesAsync(
                orchestrationInstance,
                forwardMeasurementsInput,
                receiversWithMeasurements)
            .ConfigureAwait(false);
    }

    private static ForwardMeasurementsCustomStateV1 GetForwardMeasurementsCustomState(
        OrchestrationInstance orchestrationInstance)
    {
        ForwardMeasurementsCustomStateV1 customState;
        // TODO: remove this try-catch when all orchestration instances are migrated to the new custom state
        try
        {
            customState = orchestrationInstance.CustomState.AsType<ForwardMeasurementsCustomStateV1>();
        }
        catch (InvalidOperationException)
        {
            var meteringPointMasterData = orchestrationInstance
                .CustomState
                .AsType<ForwardMeasurementsCustomStateV1>()
                .HistoricalMeteringPointMasterData
                .ToList();

            customState = new ForwardMeasurementsCustomStateV1(meteringPointMasterData);
        }

        return customState;
    }

    private async Task TerminateForwardToMeasurementStep(OrchestrationInstance orchestrationInstance)
    {
        var forwardToMeasurementStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.ForwardToMeasurementsStep);

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (forwardToMeasurementStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
            return;

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        // The alternative is that the step is pending, but we shouldn't be able to receive a "notify" event
        // from measurements if the step hasn't transitioned to running yet.
        if (forwardToMeasurementStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Forward to measurements step must be running (Id={forwardToMeasurementStep.Id}, State={forwardToMeasurementStep.Lifecycle.State}).");

        await StepHelper.TerminateStepAndCommit(forwardToMeasurementStep, _clock, _progressRepository, _telemetryClient).ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<ReceiversWithMeasurementsV1>> FindReceivers(
        OrchestrationInstance orchestrationInstance,
        ForwardMeasurementsValidInput forwardMeasurementsValidInput)
    {
        var findReceiversStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.FindReceiversStep);

        var customState = GetForwardMeasurementsCustomState(orchestrationInstance);

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (findReceiversStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
        {
            // Since the master data is saved as custom state on the orchestrationInstance, we should just
            // be able to calculate the receivers (again), based on the master data. If the inputs are the same,
            // the returned calculated receivers should also be the same.
            return CalculateReceiversWithMeasurements(customState, forwardMeasurementsValidInput);
        }

        await StepHelper.StartStepAndCommitIfPending(findReceiversStep, _clock, _progressRepository).ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (findReceiversStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Find receivers step must be running (Id={findReceiversStep.Id}, State={findReceiversStep.Lifecycle.State}).");

        var receiversWithMeasurements = CalculateReceiversWithMeasurements(customState, forwardMeasurementsValidInput);

        // Terminate Step: Find receiver step
        await StepHelper.TerminateStepAndCommit(findReceiversStep, _clock, _progressRepository, _telemetryClient).ConfigureAwait(false);

        return receiversWithMeasurements;
    }

    /// <summary>
    /// Calculate receivers with measurements based on the metering point master data and the forward measurements input.
    /// <remarks>
    /// The returned receivers should always be the same given the same inputs.
    /// </remarks>
    /// </summary>
    private List<ReceiversWithMeasurementsV1> CalculateReceiversWithMeasurements(
        ForwardMeasurementsCustomStateV1 customState,
        ForwardMeasurementsValidInput forwardMeasurementsInput)
    {
        var meteringPointMasterData = customState.HistoricalMeteringPointMasterData
            .Select(mpmd => mpmd.ToMeteringPointMasterData())
            .ToList();

        var measurements = forwardMeasurementsInput.Measurements
            .Select(
                md => new ReceiversWithMeasurements.Measurement(
                    Position: md.Position,
                    EnergyQuantity: md.EnergyQuantity,
                    QuantityQuality: md.QuantityQuality))
            .ToList();

        var receiversWithMeasurements = _meteringPointReceiversProvider
            .GetReceiversWithMeasurementsFromMasterDataList(
                new MeteringPointReceiversProvider.FindReceiversInput(
                    forwardMeasurementsInput.MeteringPointId.Value,
                    forwardMeasurementsInput.StartDateTime,
                    forwardMeasurementsInput.EndDateTime,
                    forwardMeasurementsInput.Resolution,
                    meteringPointMasterData,
                    measurements));

        return receiversWithMeasurements.ToForwardMeasurementsReceiversWithMeasurementsV1();
    }

    private async Task EnqueueAcceptedActorMessagesAsync(
        OrchestrationInstance orchestrationInstance,
        ForwardMeasurementsValidInput forwardMeasurementsInput,
        IReadOnlyCollection<ReceiversWithMeasurementsV1> receivers)
    {
        var enqueueStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (enqueueStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
            return;

        await StepHelper.StartStepAndCommitIfPending(enqueueStep, _clock, _progressRepository).ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (enqueueStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Enqueue accepted message step must be running (Id={enqueueStep.Id}, State={enqueueStep.Lifecycle.State}).");

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

        var gridAreaCode = GetForwardMeasurementsCustomState(orchestrationInstance).HistoricalMeteringPointMasterData.FirstOrDefault()?.GridAreaCode
                           ?? throw new InvalidOperationException($"Grid area code is required to enqueue accepted message with id {orchestrationInstance.Id}");

        // Enqueue forward measurements actor messages
        var data = new ForwardMeasurementsAcceptedV1(
            OriginalActorMessageId: forwardMeasurementsInput.ActorMessageId.Value,
            MeteringPointId: forwardMeasurementsInput.MeteringPointId.Value,
            MeteringPointType: forwardMeasurementsInput.MeteringPointType,
            // TODO: LRN: awaiting a decision from Team Einstein.
            ProductNumber: forwardMeasurementsInput.ProductNumber ?? "8716867000030",
            RegistrationDateTime: forwardMeasurementsInput.RegistrationDateTime.ToDateTimeOffset(),
            StartDateTime: forwardMeasurementsInput.StartDateTime.ToDateTimeOffset(),
            EndDateTime: forwardMeasurementsInput.EndDateTime.ToDateTimeOffset(),
            ReceiversWithMeasurements: receivers,
            GridAreaCode: gridAreaCode.Value);

        await _enqueueActorMessagesClient.EnqueueAsync(
                orchestration: OrchestrationDescriptionBuilder.UniqueName,
                orchestrationInstanceId: orchestrationInstance.Id.Value,
                orchestrationStartedBy: orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
                idempotencyKey: idempotencyKey,
                data: data)
            .ConfigureAwait(false);
    }
}
