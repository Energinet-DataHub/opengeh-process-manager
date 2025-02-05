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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;

/// <summary>
/// A model containing the data for an accepted request for calculated energy time series
/// </summary>
public record RequestCalculatedWholesaleServicesAcceptedV1(
    string OriginalActorMessageId,
    string OriginalTransactionId,
    ActorNumber RequestedForActorNumber,
    ActorRole RequestedForActorRole,
    ActorNumber RequestedByActorNumber,
    ActorRole RequestedByActorRole,
    BusinessReason BusinessReason,
    Resolution? Resolution,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    IReadOnlyCollection<string> GridAreas,
    ActorNumber? EnergySupplierNumber,
    ActorNumber? ChargeOwnerNumber,
    SettlementVersion? SettlementVersion,
    IReadOnlyCollection<RequestCalculatedWholesaleServicesAcceptedV1.AcceptedChargeType> ChargeTypes)
{
    public record AcceptedChargeType(
        ChargeType? ChargeType,
        string? ChargeCode);
}
