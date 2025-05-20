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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.CapacitySettlementCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.NetConsumptionCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogCalculation;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;

internal static class CalculationsQueryV1Extensions
{
    public static IReadOnlyCollection<string> GetOrchestrationDescriptionNames(this CalculationsQueryV1 query)
    {
        var orchestrationDescriptionNames = query.CalculationTypes?
            .Select(type => GetOrchestrationDescriptionName(type))
            .Distinct()
            .ToList();

        if (orchestrationDescriptionNames == null)
        {
            orchestrationDescriptionNames = [.. CalculationsQueryResultMapperV1.SupportedOrchestrationDescriptionNames];
        }

        if (query.IsInternalCalculation.HasValue && query.IsInternalCalculation == true)
        {
            orchestrationDescriptionNames.RemoveAll(name
                => name != Brs_023_027.Name);
        }

        if (query.GridAreaCodes != null && query.GridAreaCodes.Any())
        {
            orchestrationDescriptionNames.RemoveAll(name
                => name != Brs_023_027.Name);
        }

        if (query.PeriodStartDate.HasValue || query.PeriodEndDate.HasValue)
        {
            orchestrationDescriptionNames.RemoveAll(name =>
                name != Brs_023_027.Name
                && name != Brs_021_CapacitySettlementCalculation.Name);
        }

        return orchestrationDescriptionNames;
    }

    private static string GetOrchestrationDescriptionName(CalculationTypeQueryParameterV1 calculationType)
    {
        switch (calculationType)
        {
            case CalculationTypeQueryParameterV1.ElectricalHeating:
                return Brs_021_ElectricalHeatingCalculation.Name;
            case CalculationTypeQueryParameterV1.CapacitySettlement:
                return Brs_021_CapacitySettlementCalculation.Name;
            case CalculationTypeQueryParameterV1.NetConsumption:
                return Brs_021_NetConsumptionCalculation.Name;
            case CalculationTypeQueryParameterV1.MissingMeasurementsLog:
                return Brs_045_MissingMeasurementsLogCalculation.Name;
            default:
                return Brs_023_027.Name;
        }
    }
}
