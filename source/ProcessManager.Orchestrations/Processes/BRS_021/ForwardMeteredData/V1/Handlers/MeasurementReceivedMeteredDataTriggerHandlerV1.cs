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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.NotifyActorMessagesEnqueued;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class MeasurementReceivedMeteredDataTriggerHandlerV1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock,
    IEnqueueActorMessagesClient enqueueActorMessagesClient,
    INotifyToProcessManagerClient notifyToProcessManagerClient)
{
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly INotifyToProcessManagerClient _notifyToProcessManagerClient = notifyToProcessManagerClient;

    public async Task HandleAsync(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        await StepHelper.TerminateStep(
                orchestrationInstance,
                OrchestrationDescriptionBuilderV1.ForwardToMeasurementStep,
                clock,
                _progressRepository)
            .ConfigureAwait(false);

        await StepHelper.StartStep(
                orchestrationInstance,
                OrchestrationDescriptionBuilderV1.FindReceiverStep,
                clock,
                _progressRepository)
            .ConfigureAwait(false);
        await StepHelper.TerminateStep(
                orchestrationInstance,
                OrchestrationDescriptionBuilderV1.FindReceiverStep,
                clock,
                _progressRepository)
            .ConfigureAwait(false);

        await StepHelper.StartStep(
                orchestrationInstance,
                OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep,
                clock,
                _progressRepository)
            .ConfigureAwait(false);

        var data = new MeteredDataForMeteringPointAcceptedV1(
            MessageId: "MessageId",
            MeteringPointId: "MeteringPointId",
            MeteringPointType: MeteringPointType.Production,
            OriginalTransactionId: "OriginalTransactionId",
            ProductNumber: "productNumber",
            MeasureUnit: MeasurementUnit.Megawatt,
            RegistrationDateTime: clock.GetCurrentInstant().ToDateTimeOffset(),
            Resolution: Resolution.QuarterHourly,
            StartDateTime: clock.GetCurrentInstant().ToDateTimeOffset(),
            EndDateTime: clock.GetCurrentInstant().ToDateTimeOffset(),
            AcceptedEnergyObservations: new List<AcceptedEnergyObservation>
            {
                new(1, 1, Quality.Calculated),
            },
            MarketActorRecipients: [new MarketActorRecipient("8100000000115", ActorRole.EnergySupplier)]);

        var shouldInformItself = true;

        if (shouldInformItself)
        {
            // TODO: Delete this when the load test has completed.
            await _notifyToProcessManagerClient.Notify(
                new NotifyOrchestrationInstanceEvent(
                    OrchestrationInstanceId: orchestrationInstance.Id.ToString(),
                    EventName: MeteredDataForMeteringPointMessagesEnqueuedNotifyEventsV1.MeteredDataForMeteringPointMessagesEnqueuedCompleted))
                    .ConfigureAwait(false);
        }
        else
        {
            await _enqueueActorMessagesClient.EnqueueAsync(
                orchestration: OrchestrationDescriptionBuilderV1.UniqueName,
                orchestrationInstanceId: orchestrationInstanceId.Value,
                orchestrationStartedBy: orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
                idempotencyKey: Guid.NewGuid(), // TODO: fix this
                data: data).ConfigureAwait(false);
        }
    }
}
