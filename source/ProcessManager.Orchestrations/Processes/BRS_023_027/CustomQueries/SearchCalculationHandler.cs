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

using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.CustomQueries;

internal class SearchCalculationHandler(
    IOrchestrationInstanceQueries queries) :
        ISearchOrchestrationInstancesQueryHandler<CalculationQuery, CalculationQueryResult>
{
    private readonly IOrchestrationInstanceQueries _queries = queries;

    public async Task<IReadOnlyCollection<CalculationQueryResult>> HandleAsync(CalculationQuery query)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        var lifecycleState =
            Enum.TryParse<OrchestrationInstanceLifecycleState>(query.LifecycleState.ToString(), ignoreCase: true, out var lifecycleStateResult)
            ? lifecycleStateResult
            : (OrchestrationInstanceLifecycleState?)null;
        var terminationState =
            Enum.TryParse<OrchestrationInstanceTerminationState>(query.TerminationState.ToString(), ignoreCase: true, out var terminationStateResult)
            ? terminationStateResult
            : (OrchestrationInstanceTerminationState?)null;

        // DateTimeOffset values must be in "round-trip" ("o"/"O") format to be parsed correctly
        // See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier
        var startedAtOrLater = query.StartedAtOrLater.HasValue
            ? Instant.FromDateTimeOffset(query.StartedAtOrLater.Value)
            : (Instant?)null;
        var terminatedAtOrEarlier = query.TerminatedAtOrEarlier.HasValue
            ? Instant.FromDateTimeOffset(query.TerminatedAtOrEarlier.Value)
            : (Instant?)null;

        var calculations = await _queries
            .SearchAsync(
                query.OrchestrationDescriptionName,
                version: null,
                lifecycleState,
                terminationState,
                startedAtOrLater,
                terminatedAtOrEarlier)
            .ConfigureAwait(false);

        // TODO: Temporary in-memory filter on ParameterValues - should be refactored when we figure out how to pass filter objects to generic repository implementation.
        var calculationsToFilter = calculations
            .Select(x => new
            {
                OrchestrationId = x.Id,
                ParameterValue = JsonConvert.DeserializeObject<CalculationParameterValue>(x.ParameterValue.SerializedParameterValue),
            });

        var calculationTypesSet = query.CalculationTypes?.ToHashSet();
        var gridAreaCodesSet = query.GridAreaCodes?.ToHashSet();

        var filteredCalculations = calculationsToFilter
            .Where(calculation =>
                (calculationTypesSet == null || calculation.ParameterValue?.CalculationTypes?.Any(calculationTypesSet.Contains) != false) &&
                (gridAreaCodesSet == null || calculation.ParameterValue?.GridAreaCodes?.Any(gridAreaCodesSet.Contains) != false) &&
                (query.PeriodStartDate == null || calculation.ParameterValue?.PeriodStartDate >= query.PeriodStartDate) &&
                (query.PeriodEndDate == null || calculation.ParameterValue?.PeriodEndDate <= query.PeriodEndDate) &&
                (query.IsInternalCalculation == null || calculation.ParameterValue?.IsInternalCalculation == query.IsInternalCalculation))
            .Select(calculation => calculation.OrchestrationId)
            .ToHashSet();

        return calculations
            .Where(calculation => filteredCalculations.Contains(calculation.Id))
            .Select(item => new CalculationQueryResult(item.MapToTypedDto<CalculationInputV1>()))
            .ToList();
    }

    private class CalculationParameterValue()
    {
        public List<CalculationType>? CalculationTypes { get; set; }

        public List<string>? GridAreaCodes { get; set; }

        public DateTimeOffset? PeriodStartDate { get; set; }

        public DateTimeOffset? PeriodEndDate { get; set; }

        public bool? IsInternalCalculation { get; set; }
    }
}
