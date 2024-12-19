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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Mappers;

internal static class CalculationTypeMapper
{
    public static CalculationTypes FromDeltaTableValue(string calculationType)
    {
        return calculationType switch
        {
            DeltaTableCalculationType.BalanceFixing => CalculationTypes.BalanceFixing,
            DeltaTableCalculationType.Aggregation => CalculationTypes.Aggregation,
            DeltaTableCalculationType.WholesaleFixing => CalculationTypes.WholesaleFixing,
            DeltaTableCalculationType.FirstCorrectionSettlement => CalculationTypes.FirstCorrectionSettlement,
            DeltaTableCalculationType.SecondCorrectionSettlement => CalculationTypes.SecondCorrectionSettlement,
            DeltaTableCalculationType.ThirdCorrectionSettlement => CalculationTypes.ThirdCorrectionSettlement,

            _ => throw new ArgumentOutOfRangeException(
                nameof(calculationType),
                actualValue: calculationType,
                "Value does not contain a valid string representation of a calculation type."),
        };
    }

    public static string ToDeltaTableValue(CalculationTypes calculationType)
    {
        return calculationType switch
        {
            CalculationTypes.BalanceFixing => DeltaTableCalculationType.BalanceFixing,
            CalculationTypes.Aggregation => DeltaTableCalculationType.Aggregation,
            CalculationTypes.WholesaleFixing => DeltaTableCalculationType.WholesaleFixing,
            CalculationTypes.FirstCorrectionSettlement => DeltaTableCalculationType.FirstCorrectionSettlement,
            CalculationTypes.SecondCorrectionSettlement => DeltaTableCalculationType.SecondCorrectionSettlement,
            CalculationTypes.ThirdCorrectionSettlement => DeltaTableCalculationType.ThirdCorrectionSettlement,

            _ => throw new ArgumentOutOfRangeException(
                nameof(calculationType),
                actualValue: calculationType,
                "Value cannot be mapped to a string representation of a calculation type."),
        };
    }
}
