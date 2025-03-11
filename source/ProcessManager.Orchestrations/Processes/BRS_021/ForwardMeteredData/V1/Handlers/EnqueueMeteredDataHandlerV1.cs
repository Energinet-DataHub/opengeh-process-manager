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

using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using NodaTime;
using ActorNumber = Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects.ActorNumber;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class EnqueueMeteredDataHandlerV1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;

    public async Task HandleAsync(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        if (orchestrationInstance.Lifecycle.State != OrchestrationInstanceLifecycleState.Running)
            return;

        // Terminate Step: Forward metered data step
        var forwardToMeasurementStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.ForwardToMeasurementStep);
        await StepHelper.TerminateStep(forwardToMeasurementStep, _clock, _progressRepository).ConfigureAwait(false);

        // Start Step: Find receiver step
        var findReceiverStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.FindReceiverStep);
        await StepHelper.StartStep(findReceiverStep, _clock, _progressRepository).ConfigureAwait(false);

        if (findReceiverStep.Lifecycle.State == StepInstanceLifecycleState.Running)
        {
            // Find Receivers

            // Terminate Step: Find receiver step
            await StepHelper.TerminateStep(findReceiverStep, _clock, _progressRepository).ConfigureAwait(false);
        }

        // Start Step: Enqueue actor messages step
        var enqueueActorMessagesStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep);
        await StepHelper.StartStep(enqueueActorMessagesStep, _clock, _progressRepository).ConfigureAwait(false);

        if (enqueueActorMessagesStep.Lifecycle.State == StepInstanceLifecycleState.Running)
        {
            var data = new ForwardMeteredDataAcceptedV1(
                OriginalActorMessageId: "MessageId",
                MeteringPointId: "MeteringPointId",
                MeteringPointType: MeteringPointType.Production,
                OriginalTransactionId: "OriginalTransactionId",
                ProductNumber: "productNumber",
                MeasureUnit: MeasurementUnit.Megawatt,
                RegistrationDateTime: _clock.GetCurrentInstant().ToDateTimeOffset(),
                Resolution: Resolution.QuarterHourly,
                StartDateTime: _clock.GetCurrentInstant().ToDateTimeOffset(),
                EndDateTime: _clock.GetCurrentInstant().ToDateTimeOffset(),
                AcceptedEnergyObservations: new List<ForwardMeteredDataAcceptedV1.AcceptedEnergyObservation>
                {
                    new(1, 1, Quality.Calculated),
                },
                MarketActorRecipients: [new MarketActorRecipientV1(ActorNumber.Create("8100000000115"), ActorRole.EnergySupplier)]);

            await _enqueueActorMessagesClient.EnqueueAsync(
                orchestration: OrchestrationDescriptionBuilderV1.UniqueName,
                orchestrationInstanceId: orchestrationInstanceId.Value,
                orchestrationStartedBy: orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
                idempotencyKey: Guid.NewGuid(), // TODO: fix this
                data: data).ConfigureAwait(false);
        }
    }
}
