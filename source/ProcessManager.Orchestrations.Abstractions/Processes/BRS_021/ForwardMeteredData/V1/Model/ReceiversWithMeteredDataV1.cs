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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

/// <summary>
/// Describes how metered data is grouped (sent to) market actors for specific time periods, resolutions and measure units.
/// </summary>
/// <param name="Actors">The actors who should receive the metered data for the given period.</param>
/// <param name="Resolution">The resolution the metered data has in this period.</param>
/// <param name="MeasureUnit">The measure units the metered data has in this period.</param>
/// <param name="StartDateTime">The start date and time of the period.</param>
/// <param name="EndDateTime">The end date and time of the period.</param>
/// <param name="MeteredData">The metered data in the given period.</param>
public record ReceiversWithMeteredDataV1(
    IReadOnlyCollection<MarketActorRecipientV1> Actors,
    Resolution Resolution,
    MeasurementUnit MeasureUnit,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    IReadOnlyCollection<ReceiversWithMeteredDataV1.AcceptedMeteredData> MeteredData)
{
    public record AcceptedMeteredData(
        int Position,
        decimal? EnergyQuantity,
        Quality? QuantityQuality);
}
