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

namespace Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;

/// <summary>
/// Describes how measurements is grouped (sent to) market actors for specific time periods, resolutions and measure units.
/// </summary>
/// <param name="Receivers">The actors who should receive the measurements for the given period.</param>
/// <param name="Resolution">The resolution the measurements has in this period.</param>
/// <param name="MeasureUnit">The measure units the measurements has in this period.</param>
/// <param name="StartDateTime">The start date and time of the period.</param>
/// <param name="EndDateTime">The end date and time of the period.</param>
/// <param name="GridArea">The grid area code.</param>
/// <param name="Measurements">The measurements in the given period.</param>
public record ReceiversWithMeasurements(
    IReadOnlyCollection<Actor> Receivers,
    Resolution Resolution,
    MeasurementUnit MeasureUnit,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string GridArea,
    IReadOnlyCollection<ReceiversWithMeasurements.Measurement> Measurements)
{
    public record Measurement(
        int Position,
        decimal? EnergyQuantity,
        Quality? QuantityQuality);
}
