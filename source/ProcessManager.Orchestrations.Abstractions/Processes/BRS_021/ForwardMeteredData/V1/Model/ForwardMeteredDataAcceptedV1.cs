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
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

/// <summary>
/// A model containing the data required for notifying market actors when new metered data has been accepted.
/// </summary>
public record ForwardMeteredDataAcceptedV1(
    string OriginalActorMessageId,
    string OriginalTransactionId,
    string MeteringPointId,
    MeteringPointType MeteringPointType,
    string ProductNumber,
    MeasurementUnit MeasureUnit,
    DateTimeOffset RegistrationDateTime,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    IReadOnlyCollection<ForwardMeteredDataAcceptedV1.MeteredDataForReceiver> Recipients)
    : IInputParameterDto
{
    public record MeteredDataForReceiver(
        IReadOnlyCollection<MarketActorRecipientV1> Recipient,
        Resolution Resolution,
        DateTimeOffset StartDateTime,
        DateTimeOffset EndDateTime,
        IReadOnlyCollection<AcceptedMeteredData> MeteredData);

    public record AcceptedMeteredData(
        int Position,
        // TODO: Are these two optional?
        decimal? EnergyQuantity,
        Quality? QuantityQuality);
}
