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

using Energinet.DataHub.ProcessManager.Components.Time;
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.CapacitySettlementCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.NetConsumptionCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;

internal class SearchCalculationsHandlerV1(
    ProcessManagerReaderContext readerContext,
    TimeHelper timeHelper) :
        ISearchOrchestrationInstancesQueryHandler<CalculationsQueryV1, ICalculationsQueryResultV1>
{
    private readonly ProcessManagerReaderContext _readerContext = readerContext;
    private readonly TimeHelper _timeHelper = timeHelper;

    public async Task<IReadOnlyCollection<ICalculationsQueryResultV1>> HandleAsync(CalculationsQueryV1 query)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *

        var orchestrationDescriptionNames = query.GetOrchestrationDescriptionNames();

        var lifecycleStates = query.LifecycleStates.MapToDomain();
        var terminationState = query.TerminationState.MapToDomain();

        var scheduledAtOrLater = query.ScheduledAtOrLater.ToNullableInstant();
        var startedAtOrLater = query.StartedAtOrLater.ToNullableInstant();
        var terminatedAtOrEarlier = query.TerminatedAtOrEarlier.ToNullableInstant();

        var results =
            await SearchAsync(
                orchestrationDescriptionNames,
                lifecycleStates,
                terminationState,
                scheduledAtOrLater,
                startedAtOrLater,
                terminatedAtOrEarlier)
            .ConfigureAwait(false);

        return results
            // TODO: Temporary in-memory filter on ParameterValues.
            // Should be refactored when we figure out how to pass filter objects to generic repository implementation.
            .Where(item =>
                item.UniqueName.Name != Brs_023_027.Name
                || (item.UniqueName.Name == Brs_023_027.Name && FilterBrs_023_027(query, item.Instance)))
            // TODO: Temporary in-memory filter on ParameterValues.
            // Should be refactored when we figure out how to pass filter objects to generic repository implementation.
            .Where(item =>
                item.UniqueName.Name != Brs_021_CapacitySettlementCalculation.Name
                || (item.UniqueName.Name == Brs_021_CapacitySettlementCalculation.Name && FilterBrs_021_CapacitySettlement(query, item.Instance)))
            .Select(item => MapToConcreteResultDto(item.UniqueName, item.Instance))
            .ToList();
    }

    private static bool FilterBrs_023_027(CalculationsQueryV1 query, OrchestrationInstance orchestrationInstance)
    {
        var calculationInput = orchestrationInstance.ParameterValue
            .AsType<Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1>();
        var calculationTypesAsInt = query.CalculationTypes?.Select(type => (int)type).ToList();

        return
            (calculationTypesAsInt == null || calculationTypesAsInt.Contains((int)calculationInput.CalculationType)) &&
            (query.GridAreaCodes == null || calculationInput.GridAreaCodes.Any(query.GridAreaCodes.Contains)) &&
            // This period check follows the algorithm "bool overlap = a.start < b.end && b.start < a.end"
            // where a = query and b = calculationInput.
            // See https://stackoverflow.com/questions/13513932/algorithm-to-detect-overlapping-periods for more info.
            (query.PeriodStartDate == null || query.PeriodStartDate < calculationInput.PeriodEndDate) &&
            (query.PeriodEndDate == null || calculationInput.PeriodStartDate < query.PeriodEndDate) &&
            (query.IsInternalCalculation == null || calculationInput.IsInternalCalculation == query.IsInternalCalculation);
    }

    private static ICalculationsQueryResultV1 MapToConcreteResultDto(OrchestrationDescriptionUniqueName uniqueName, OrchestrationInstance instance)
    {
        switch (uniqueName.Name)
        {
            case Brs_023_027.Name:
                var wholesale = instance.MapToTypedDto<Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1>();
                return new WholesaleCalculationResultV1(
                    wholesale.Id,
                    wholesale.Lifecycle,
                    wholesale.Steps,
                    wholesale.CustomState,
                    wholesale.ParameterValue);

            case Brs_021_ElectricalHeatingCalculation.Name:
                var electricalHeating = instance.MapToDto();
                return new ElectricalHeatingCalculationResultV1(
                    electricalHeating.Id,
                    electricalHeating.Lifecycle,
                    electricalHeating.Steps,
                    electricalHeating.CustomState);

            case Brs_021_CapacitySettlementCalculation.Name:
                var capacitySettlement = instance.MapToTypedDto<Abstractions.Processes.BRS_021.CapacitySettlementCalculation.V1.Model.CalculationInputV1>();
                return new CapacitySettlementCalculationResultV1(
                    capacitySettlement.Id,
                    capacitySettlement.Lifecycle,
                    capacitySettlement.Steps,
                    capacitySettlement.CustomState,
                    capacitySettlement.ParameterValue);

            case Brs_021_NetConsumptionCalculation.Name:
                var netConsumption = instance.MapToDto();
                return new NetConsumptionCalculationResultV1(
                    netConsumption.Id,
                    netConsumption.Lifecycle,
                    netConsumption.Steps,
                    netConsumption.CustomState);

            default:
                throw new InvalidOperationException($"Unsupported unique name '{uniqueName.Name}'.");
        }
    }

    private bool FilterBrs_021_CapacitySettlement(CalculationsQueryV1 query, OrchestrationInstance orchestrationInstance)
    {
        var calculationInput = orchestrationInstance.ParameterValue
            .AsType<Abstractions.Processes.BRS_021.CapacitySettlementCalculation.V1.Model.CalculationInputV1>();

        var year = (int)calculationInput.Year;
        var month = (int)calculationInput.Month;

        var startDate = Instant.FromDateTimeOffset(new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero));
        startDate = _timeHelper.GetMidnightZonedDateTime(startDate);
        var calculationInputPeriodStart = startDate.ToDateTimeOffset();

        var endDate = Instant.FromDateTimeOffset(new DateTimeOffset(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59, TimeSpan.Zero));
        endDate = _timeHelper.GetMidnightZonedDateTime(endDate);
        var calculationInputPeriodEnd = endDate.ToDateTimeOffset();

        return
            // This period check follows the algorithm "bool overlap = a.start < b.end && b.start < a.end"
            // where a = query and b = calculationInput.
            // See https://stackoverflow.com/questions/13513932/algorithm-to-detect-overlapping-periods for more info.
            (query.PeriodStartDate == null || query.PeriodStartDate < calculationInputPeriodEnd) &&
            (query.PeriodEndDate == null || calculationInputPeriodStart < query.PeriodEndDate);
    }

    /// <summary>
    /// Get all orchestration instances filtered by their orchestration description name
    /// and lifecycle information.
    /// </summary>
    /// <param name="orchestrationDescriptionNames"></param>
    /// <param name="lifecycleStates"></param>
    /// <param name="terminationState"></param>
    /// <param name="scheduledAtOrLater"></param>
    /// <param name="startedAtOrLater"></param>
    /// <param name="terminatedAtOrEarlier"></param>
    /// <returns>Use the returned unique name to determine which orchestration description
    /// a given orchestration instance was created from.</returns>
    private async Task<IReadOnlyCollection<(OrchestrationDescriptionUniqueName UniqueName, OrchestrationInstance Instance)>> SearchAsync(
        IReadOnlyCollection<string> orchestrationDescriptionNames,
        IReadOnlyCollection<OrchestrationInstanceLifecycleState>? lifecycleStates,
        OrchestrationInstanceTerminationState? terminationState,
        Instant? scheduledAtOrLater,
        Instant? startedAtOrLater,
        Instant? terminatedAtOrEarlier)
    {
        var queryable = _readerContext
            .OrchestrationDescriptions
                .Where(x => orchestrationDescriptionNames.Contains(x.UniqueName.Name))
            .Join(
                _readerContext.OrchestrationInstances,
                description => description.Id,
                instance => instance.OrchestrationDescriptionId,
                (description, instance) => new { description.UniqueName, instance })
            .Where(x => lifecycleStates == null || lifecycleStates.Contains(x.instance.Lifecycle.State))
            .Where(x => terminationState == null || x.instance.Lifecycle.TerminationState == terminationState)
            .Where(x => startedAtOrLater == null || startedAtOrLater <= x.instance.Lifecycle.StartedAt)
            .Where(x => terminatedAtOrEarlier == null || x.instance.Lifecycle.TerminatedAt <= terminatedAtOrEarlier)
            .Where(x => scheduledAtOrLater == null || scheduledAtOrLater <= x.instance.Lifecycle.ScheduledToRunAt)
            .Select(x => ValueTuple.Create(x.UniqueName, x.instance));

#if DEBUG
        var queryStringForDebugging = queryable.ToQueryString();
#endif

        return await queryable.ToListAsync().ConfigureAwait(false);
    }
}
