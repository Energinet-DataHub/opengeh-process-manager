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
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.FeatureManagement;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.ApplicationInsights;
using Microsoft.FeatureManagement;
using NodaTime;
using OrchestrationInstanceLifecycleState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceLifecycleState;
using StepInstanceLifecycleState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.StepInstanceLifecycleState;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class EnqueueMeasurementsHandlerV1(
    IOrchestrationInstanceProgressRepository progressRepository,
    ISendMeasurementsInstanceRepository sendMeasurementsInstanceRepository,
    IClock clock,
    IEnqueueActorMessagesClient enqueueActorMessagesClient,
    MeteringPointReceiversProvider meteringPointReceiversProvider,
    TelemetryClient telemetryClient,
    IFeatureManager featureManager)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly ISendMeasurementsInstanceRepository _sendMeasurementsInstanceRepository = sendMeasurementsInstanceRepository;
    private readonly IClock _clock = clock;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly MeteringPointReceiversProvider _meteringPointReceiversProvider = meteringPointReceiversProvider;
    private readonly TelemetryClient _telemetryClient = telemetryClient;
    private readonly IFeatureManager _featureManager = featureManager;

    public async Task HandleAsync(Guid instanceId)
    {
        var useNewSendMeasurementsTable = await _featureManager.UseNewSendMeasurementsTable().ConfigureAwait(false);

        if (useNewSendMeasurementsTable)
        {
            await HandleSendMeasurementsInstanceAsync(SendMeasurementsInstanceId.FromExisting(instanceId))
                .ConfigureAwait(false);
        }
        else
        {
            await HandleOrchestrationInstanceAsync(new OrchestrationInstanceId(instanceId))
                .ConfigureAwait(false);
        }
    }

    public async Task HandleSendMeasurementsInstanceAsync(SendMeasurementsInstanceId sendMeasurementsInstanceId)
    {
        var instance = await _sendMeasurementsInstanceRepository
            .GetAsync(sendMeasurementsInstanceId)
            .ConfigureAwait(false);

        // If the instance is terminated, do nothing (idempotency/retry check).
        if (instance.Lifecycle.State is OrchestrationInstanceLifecycleState.Terminated)
            return;

        // If the instance is already sent to enqueue actor messages, do nothing (idempotency/retry check).
        if (instance.IsSentToEnqueueActorMessages)
            return;

        // Instance can already be marked as received from measurements, so we check that first (idempotency/retry check).
        if (!instance.IsReceivedFromMeasurements)
        {
            instance.MarkAsReceivedFromMeasurements(_clock.GetCurrentInstant());
            await _sendMeasurementsInstanceRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

        var inputStream = await _sendMeasurementsInstanceRepository.DownloadInputAsync(instance.FileStorageReference).ConfigureAwait(false);
        var input = await JsonSerializer.DeserializeAsync<ForwardMeteredDataInputV1>(inputStream.Stream).ConfigureAwait(false);

        if (input is null)
            throw new InvalidOperationException($"Failed to deserialize input for SendMeasurementsInstance (Id={instance.Id}).");

        // Only valid data will be enqueued
        var forwardMeteredDataInput = ForwardMeteredDataValidInput.From(input);

        var masterDataCustomState = instance.MasterData.AsType<ForwardMeteredDataCustomStateV2>();

        // Start Step: Find receiver step
        var receiversWithMeteredData = FindReceivers(masterDataCustomState, forwardMeteredDataInput);

        // Start Step: Enqueue actor messages step
        await EnqueueAcceptedActorMessagesAsync(
                instance,
                forwardMeteredDataInput,
                masterDataCustomState,
                receiversWithMeteredData)
            .ConfigureAwait(false);
    }

    private async Task HandleOrchestrationInstanceAsync(OrchestrationInstanceId orchestrationInstanceId)
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
        var forwardMeteredDataInput = ForwardMeteredDataValidInput.From(orchestrationInstance.ParameterValue.AsType<ForwardMeteredDataInputV1>());

        await TerminateForwardToMeasurementStep(orchestrationInstance).ConfigureAwait(false);

        // Start Step: Find receiver step
        var receiversWithMeteredData = await FindReceivers(orchestrationInstance, forwardMeteredDataInput).ConfigureAwait(false);

        // Start Step: Enqueue actor messages step
        await EnqueueAcceptedActorMessagesAsync(
                orchestrationInstance,
                forwardMeteredDataInput,
                receiversWithMeteredData)
            .ConfigureAwait(false);
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

    private async Task<IReadOnlyCollection<ReceiversWithMeteredDataV1>> FindReceivers(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataValidInput forwardMeteredDataValidInput)
    {
        var findReceiversStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.FindReceiversStep);

        var customState = orchestrationInstance.CustomState.AsType<ForwardMeteredDataCustomStateV2>();

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (findReceiversStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
        {
            // Since the master data is saved as custom state on the orchestrationInstance, we should just
            // be able to calculate the receivers (again), based on the master data. If the inputs are the same,
            // the returned calculated receivers should also be the same.
            return CalculateReceiversWithMeasurements(customState, forwardMeteredDataValidInput);
        }

        await StepHelper.StartStepAndCommitIfPending(findReceiversStep, _clock, _progressRepository).ConfigureAwait(false);

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        if (findReceiversStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Find receivers step must be running (Id={findReceiversStep.Id}, State={findReceiversStep.Lifecycle.State}).");

        var receiversWithMeteredData = CalculateReceiversWithMeasurements(customState, forwardMeteredDataValidInput);

        // Terminate Step: Find receiver step
        await StepHelper.TerminateStepAndCommit(findReceiversStep, _clock, _progressRepository, _telemetryClient).ConfigureAwait(false);

        return receiversWithMeteredData;
    }

    private IReadOnlyCollection<ReceiversWithMeteredDataV1> FindReceivers(
        ForwardMeteredDataCustomStateV2 customState,
        ForwardMeteredDataValidInput forwardMeteredDataValidInput)
    {
        var receiversWithMeteredData = CalculateReceiversWithMeasurements(
            customState,
            forwardMeteredDataValidInput);

        return receiversWithMeteredData;
    }

    /// <summary>
    /// Calculate receivers with measurements based on the metering point master data and the forward metered data input.
    /// <remarks>
    /// The returned receivers should always be the same given the same inputs.
    /// </remarks>
    /// </summary>
    private List<ReceiversWithMeteredDataV1> CalculateReceiversWithMeasurements(
        ForwardMeteredDataCustomStateV2 customState,
        ForwardMeteredDataValidInput forwardMeteredDataInput)
    {
        var meteringPointMasterData = customState.HistoricalMeteringPointMasterData
            .Select(mpmd => mpmd.ToMeteringPointMasterData())
            .ToList();

        var measurements = forwardMeteredDataInput.Measurements
            .Select(
                md => new ReceiversWithMeasurements.Measurement(
                    Position: md.Position,
                    EnergyQuantity: md.EnergyQuantity,
                    QuantityQuality: md.QuantityQuality))
            .ToList();

        var receiversWithMeasurements = _meteringPointReceiversProvider
            .GetReceiversWithMeasurementsFromMasterDataList(
                new MeteringPointReceiversProvider.FindReceiversInput(
                    forwardMeteredDataInput.MeteringPointId.Value,
                    forwardMeteredDataInput.StartDateTime,
                    forwardMeteredDataInput.EndDateTime,
                    forwardMeteredDataInput.Resolution,
                    meteringPointMasterData,
                    measurements));

        if (customState.AdditionalRecipients.Any())
        {
            receiversWithMeasurements.Add(
                new ReceiversWithMeasurements(
                    Receivers: customState.AdditionalRecipients,
                    Resolution: forwardMeteredDataInput.Resolution,
                    MeasureUnit: forwardMeteredDataInput.MeasureUnit,
                    StartDateTime: forwardMeteredDataInput.StartDateTime.ToDateTimeOffset(),
                    EndDateTime: forwardMeteredDataInput.EndDateTime.ToDateTimeOffset(),
                    GridArea: meteringPointMasterData.First().CurrentGridAreaCode.Value,
                    Measurements: measurements));
        }

        return receiversWithMeasurements.ToForwardMeteredDataReceiversWithMeasurementsV1();
    }

    private async Task EnqueueAcceptedActorMessagesAsync(
        OrchestrationInstance orchestrationInstance,
        ForwardMeteredDataValidInput input,
        IReadOnlyCollection<ReceiversWithMeteredDataV1> receivers)
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

        var customState = orchestrationInstance.CustomState.AsType<ForwardMeteredDataCustomStateV2>();

        var gridAreaCode = customState.HistoricalMeteringPointMasterData.FirstOrDefault()?.GridAreaCode
                           ?? throw new InvalidOperationException($"Grid area code is required to enqueue accepted message with id {orchestrationInstance.Id}");

        // Enqueue forward metered data actor messages
        var data = new ForwardMeteredDataAcceptedV1(
            OriginalActorMessageId: input.ActorMessageId.Value,
            MeteringPointId: input.MeteringPointId.Value,
            MeteringPointType: input.MeteringPointType,
            ProductNumber: input.ProductNumber,
            RegistrationDateTime: input.RegistrationDateTime.ToDateTimeOffset(),
            StartDateTime: input.StartDateTime.ToDateTimeOffset(),
            EndDateTime: input.EndDateTime.ToDateTimeOffset(),
            ReceiversWithMeteredData: receivers,
            GridAreaCode: gridAreaCode.Value);

        await _enqueueActorMessagesClient.EnqueueAsync(
                orchestration: Brs_021_ForwardedMeteredData.V1,
                orchestrationInstanceId: orchestrationInstance.Id.Value,
                orchestrationStartedBy: orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
                idempotencyKey: idempotencyKey,
                data: data)
            .ConfigureAwait(false);
    }

    private async Task EnqueueAcceptedActorMessagesAsync(
        SendMeasurementsInstance instance,
        ForwardMeteredDataValidInput input,
        ForwardMeteredDataCustomStateV2 customState,
        IReadOnlyCollection<ReceiversWithMeteredDataV1> receivers)
    {
        // Ensure always using the same idempotency key. Messages will only be enqueued once per instance,
        // so we can use the instance id as the idempotency key.
        var idempotencyKey = instance.Id.Value;

        var gridAreaCode = customState.HistoricalMeteringPointMasterData.FirstOrDefault()?.GridAreaCode
                           ?? throw new InvalidOperationException($"Grid area code is required to enqueue accepted message with id {instance.Id}");

        // Enqueue forward metered data actor messages
        var data = new ForwardMeteredDataAcceptedV1(
            OriginalActorMessageId: input.ActorMessageId.Value,
            MeteringPointId: input.MeteringPointId.Value,
            MeteringPointType: input.MeteringPointType,
            ProductNumber: input.ProductNumber,
            RegistrationDateTime: input.RegistrationDateTime.ToDateTimeOffset(),
            StartDateTime: input.StartDateTime.ToDateTimeOffset(),
            EndDateTime: input.EndDateTime.ToDateTimeOffset(),
            ReceiversWithMeteredData: receivers,
            GridAreaCode: gridAreaCode.Value);

        await _enqueueActorMessagesClient.EnqueueAsync(
                orchestration: Brs_021_ForwardedMeteredData.V1,
                orchestrationInstanceId: instance.Id.Value,
                orchestrationStartedBy: new ActorIdentityDto(
                    instance.CreatedByActorNumber,
                    instance.CreatedByActorRole),
                idempotencyKey: idempotencyKey,
                data: data)
            .ConfigureAwait(false);

        instance.MarkAsSentToEnqueueActorMessages(_clock.GetCurrentInstant());
        await _sendMeasurementsInstanceRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }
}
