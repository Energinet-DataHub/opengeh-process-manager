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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.CapacitySettlementCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.NetConsumptionCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;

// TODO: We might be able to retrieve come of this code to reuse it for other queries
internal static class CalculationsQueryV1Extensions
{
    /// <summary>
    /// DateTimeOffset values must be in "round-trip" ("o"/"O") format to be parsed correctly
    /// See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier
    /// </summary>
    public static Instant? ConvertToNullableInstant(this DateTimeOffset? dateTimeOffset)
    {
        return dateTimeOffset.HasValue
            ? Instant.FromDateTimeOffset(dateTimeOffset.Value)
            : (Instant?)null;
    }

    public static IReadOnlyCollection<OrchestrationInstanceLifecycleState>? GetLifecycleStates(this CalculationsQueryV1 query)
    {
        return query.LifecycleStates?
            .Select(state =>
                Enum.TryParse<OrchestrationInstanceLifecycleState>(state.ToString(), ignoreCase: true, out var lifecycleStateResult)
                ? lifecycleStateResult
                : (OrchestrationInstanceLifecycleState?)null)
            .Where(state => state.HasValue)
            .Select(state => state!.Value)
            .ToList();
    }

    public static OrchestrationInstanceTerminationState? GetTerminationState(this CalculationsQueryV1 query)
    {
        return Enum.TryParse<OrchestrationInstanceTerminationState>(query.TerminationState.ToString(), ignoreCase: true, out var terminationStateResult)
            ? terminationStateResult
            : (OrchestrationInstanceTerminationState?)null;
    }

    public static IReadOnlyCollection<string> GetOrchestrationDescriptionNames(this CalculationsQueryV1 query)
    {
        var orchestrationDescriptionNames = query.CalculationTypes?
            .Select(type => GetOrchestrationDescriptionName(type))
            .Distinct()
            .ToList();

        if (orchestrationDescriptionNames == null)
        {
            orchestrationDescriptionNames = [
                Brs_021_ElectricalHeatingCalculation.Name,
                Brs_021_CapacitySettlementCalculation.Name,
                Brs_021_NetConsumptionCalculation.Name,
                Brs_023_027.Name];
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
            default:
                return Brs_023_027.Name;
        }
    }
}
