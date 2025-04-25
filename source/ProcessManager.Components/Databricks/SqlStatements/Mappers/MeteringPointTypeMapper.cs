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

namespace Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements.Mappers;

/// <summary>
/// This class can map from the metering point types that are used in the delta tables described here:
/// https://energinet.atlassian.net/wiki/spaces/D3/pages/1014202369/Wholesale+Results
/// </summary>
public static class MeteringPointTypeMapper
{
    public static MeteringPointType FromDeltaTableValue(string? meteringPointType)
    {
        return meteringPointType switch
        {
            "consumption" => MeteringPointType.Consumption,
            "production" => MeteringPointType.Production,
            "exchange" => MeteringPointType.Exchange,

            "ve_production" => MeteringPointType.VeProduction,
            "net_production" => MeteringPointType.NetProduction,
            "supply_to_grid" => MeteringPointType.SupplyToGrid,
            "consumption_from_grid" => MeteringPointType.ConsumptionFromGrid,
            "wholesale_services_information" => MeteringPointType.WholesaleServicesInformation,
            "own_production" => MeteringPointType.OwnProduction,
            "net_from_grid" => MeteringPointType.NetFromGrid,
            "net_to_grid" => MeteringPointType.NetToGrid,
            "total_consumption" => MeteringPointType.TotalConsumption,
            "electrical_heating" => MeteringPointType.ElectricalHeating,
            "net_consumption" => MeteringPointType.NetConsumption,
            "capacity_settlement" => MeteringPointType.CapacitySettlement,

            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(meteringPointType),
                actualValue: meteringPointType,
                "Value does not contain a valid string representation of a metering point type."),
        };
    }
}
