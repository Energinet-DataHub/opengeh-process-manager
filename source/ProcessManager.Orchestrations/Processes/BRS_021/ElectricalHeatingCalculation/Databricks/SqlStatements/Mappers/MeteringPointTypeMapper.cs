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
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements.Mappers;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements.Mappers;

// TODO - XDAST: We will investigate if we can implement a "common" reusable mapper, but for now we use this for Electrical Heating only
internal static class MeteringPointTypeMapper
{
    public static MeteringPointType FromDeltaTableValue(string? meteringPointType)
    {
        return meteringPointType switch
        {
            DeltaTableMeteringPointType.Consumption => MeteringPointType.CapacitySettlement,
            DeltaTableMeteringPointType.Production => MeteringPointType.ElectricalHeating,
            DeltaTableMeteringPointType.Exchange => MeteringPointType.NetConsumption,

            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(meteringPointType),
                actualValue: meteringPointType,
                "Value does not contain a valid string representation of a metering point type."),
        };
    }
}
