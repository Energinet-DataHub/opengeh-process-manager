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

        var lifecycleStates = query.LifecycleStates.MapToDomain();
        var terminationState = query.TerminationState.MapToDomain();

        var scheduledAtOrLater = query.ScheduledAtOrLater.ToNullableInstant();
        var startedAtOrLater = query.StartedAtOrLater.ToNullableInstant();
        var terminatedAtOrEarlier = query.TerminatedAtOrEarlier.ToNullableInstant();

        var calculations = await _queries
            .SearchAsync(
                query.OrchestrationDescriptionName,
                version: null,
                lifecycleStates,
                terminationState,
                startedAtOrLater,
                terminatedAtOrEarlier,
                scheduledAtOrLater)
            .ConfigureAwait(false);

        // TODO: Temporary in-memory filter on ParameterValues - should be refactored when we figure out how to pass filter objects to generic repository implementation.
        var filteredCalculations = calculations
            .Where(instance => FilterCalculation(instance, query))
            .Select(calculation => new CalculationQueryResult(calculation.MapToTypedDto<CalculationInputV1>()))
            .ToList();

        return filteredCalculations;
    }

    private bool FilterCalculation(OrchestrationInstance orchestrationInstance, CalculationQuery query)
    {
        var calculationParameters = orchestrationInstance.ParameterValue.AsType<CalculationInputV1>();

        return (query.CalculationTypes == null || query.CalculationTypes.Contains(calculationParameters.CalculationType)) &&
                (query.GridAreaCodes == null || calculationParameters.GridAreaCodes.Any(query.GridAreaCodes.Contains)) &&
                // This period check follows the algorithm "bool overlap = a.start < b.end && b.start < a.end"
                // where a = query and b = calculationParameters.
                // See https://stackoverflow.com/questions/13513932/algorithm-to-detect-overlapping-periods for more info.
                (query.PeriodStartDate == null || query.PeriodStartDate < calculationParameters.PeriodEndDate) &&
                (query.PeriodEndDate == null || calculationParameters.PeriodStartDate < query.PeriodEndDate) &&
                (query.IsInternalCalculation == null || calculationParameters.IsInternalCalculation == query.IsInternalCalculation);
    }
}
