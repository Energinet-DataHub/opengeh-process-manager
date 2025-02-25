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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V2.Handlers;

public class MeasurementReceivedMeteredDataTriggerHandlerV2(
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;

    public async Task HandleAsync(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        await StepHelper.TerminateStep(
                orchestrationInstance,
                OrchestrationDescriptionBuilder.ForwardToMeasurementStep,
                clock,
                _progressRepository)
            .ConfigureAwait(false);
        await StepHelper.StartStep(
                orchestrationInstance,
                OrchestrationDescriptionBuilder.EnqueueActorMessagesStep,
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
                new AcceptedEnergyObservation(1, 1, Quality.Calculated),
            },
            MarketActorRecipients: [new MarketActorRecipient("8100000000115", ActorRole.EnergySupplier)]);

        // TODO: Do we want to inform our own trigger and not EDI?
        await _enqueueActorMessagesClient.EnqueueAsync(
            orchestration: OrchestrationDescriptionBuilder.UniqueName,
            orchestrationInstanceId: orchestrationInstanceId.Value,
            orchestrationStartedBy: orchestrationInstance.Lifecycle.CreatedBy.Value.ToDto(),
            idempotencyKey: Guid.NewGuid(), // TODO: fix this
            data: data).ConfigureAwait(false);
    }
}
