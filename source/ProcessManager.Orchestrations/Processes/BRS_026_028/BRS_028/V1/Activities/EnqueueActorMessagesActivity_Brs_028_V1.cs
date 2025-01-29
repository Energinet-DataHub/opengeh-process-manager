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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Microsoft.Azure.Functions.Worker;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Activities;

/// <summary>
/// Enqueue messages in EDI (and set step to running)
/// </summary>
internal class EnqueueActorMessagesActivity_Brs_028_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueActorMessagesActivity_Brs_028_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        await EnqueueActorMessagesAsync(orchestrationInstance.Lifecycle.CreatedBy.Value, input).ConfigureAwait(false);
    }

    private Task EnqueueActorMessagesAsync(OperatingIdentity enqueuedBy, ActivityInput input)
    {
        var requestInput = input.RequestInput;

        var energySupplierNumber = requestInput.EnergySupplierNumber != null
            ? ActorNumber.Create(requestInput.EnergySupplierNumber)
            : null;
        var chargeOwnerNumber = requestInput.ChargeOwnerNumber != null
            ? ActorNumber.Create(requestInput.ChargeOwnerNumber)
            : null;
        var settlementVersion = requestInput.SettlementVersion != null
            ? SettlementVersion.FromName(requestInput.SettlementVersion)
            : null;

        var acceptedData = new RequestCalculatedWholesaleServicesAcceptedV1(
            OriginalTransactionId: requestInput.TransactionId,
            OriginalMessageId: requestInput.ActorMessageId,
            RequestedForActorNumber: ActorNumber.Create(requestInput.RequestedForActorNumber),
            RequestedForActorRole: ActorRole.FromName(requestInput.RequestedForActorRole),
            RequestedByActorNumber: ActorNumber.Create(requestInput.RequestedByActorNumber),
            RequestedByActorRole: ActorRole.FromName(requestInput.RequestedByActorRole),
            BusinessReason: BusinessReason.FromName(requestInput.BusinessReason),
            PeriodStart: InstantPattern.General.Parse(requestInput.PeriodStart).GetValueOrThrow().ToDateTimeOffset(),
            PeriodEnd: InstantPattern.General.Parse(requestInput.PeriodEnd!).GetValueOrThrow().ToDateTimeOffset(),
            GridAreas: requestInput.GridAreas,
            EnergySupplierNumber: energySupplierNumber,
            ChargeOwnerNumber: chargeOwnerNumber,
            SettlementVersion: settlementVersion,
            ChargeTypes: requestInput.ChargeTypes ?? []);

        return _enqueueActorMessagesClient.EnqueueAsync(
            Orchestration_Brs_028_V1.UniqueName,
            input.InstanceId.Value,
            enqueuedBy.ToDto(),
            input.IdempotencyKey,
            acceptedData);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        RequestCalculatedWholesaleServicesInputV1 RequestInput,
        Guid IdempotencyKey);
}
