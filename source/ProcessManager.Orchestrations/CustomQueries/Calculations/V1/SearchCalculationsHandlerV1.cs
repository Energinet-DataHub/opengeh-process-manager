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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.CapacitySettlementCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;

internal class SearchCalculationsHandlerV1(
    ProcessManagerReaderContext readerContext) :
        ISearchOrchestrationInstancesQueryHandler<CalculationsQueryV1, ICalculationsQueryResultV1>
{
    private readonly ProcessManagerReaderContext _readerContext = readerContext;

    public async Task<IReadOnlyCollection<ICalculationsQueryResultV1>> HandleAsync(CalculationsQueryV1 query)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *

        // TODO: Refact; for now we hardcode this list, but it should be built based on input
        IReadOnlyCollection<string> orchestrationDescriptionNames = [
            Brs_023_027.Name,
            Brs_021_ElectricalHeatingCalculation.Name,
            Brs_021_CapacitySettlementCalculation.Name];

        var lifecycleStates = query.LifecycleStates?
            .Select(state =>
                Enum.TryParse<OrchestrationInstanceLifecycleState>(state.ToString(), ignoreCase: true, out var lifecycleStateResult)
                ? lifecycleStateResult
                : (OrchestrationInstanceLifecycleState?)null)
            .Where(state => state.HasValue)
            .Select(state => state!.Value)
            .ToList();
        var terminationState =
            Enum.TryParse<OrchestrationInstanceTerminationState>(query.TerminationState.ToString(), ignoreCase: true, out var terminationStateResult)
            ? terminationStateResult
            : (OrchestrationInstanceTerminationState?)null;

        // DateTimeOffset values must be in "round-trip" ("o"/"O") format to be parsed correctly
        // See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier
        var scheduledAtOrLater = query.ScheduledAtOrLater.HasValue
            ? Instant.FromDateTimeOffset(query.ScheduledAtOrLater.Value)
            : (Instant?)null;
        var startedAtOrLater = query.StartedAtOrLater.HasValue
            ? Instant.FromDateTimeOffset(query.StartedAtOrLater.Value)
            : (Instant?)null;
        var terminatedAtOrEarlier = query.TerminatedAtOrEarlier.HasValue
            ? Instant.FromDateTimeOffset(query.TerminatedAtOrEarlier.Value)
            : (Instant?)null;

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
            .Select(item => MapToConcreteResultDto(item.UniqueName, item.Instance))
            .ToList();
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

        return await queryable.ToListAsync().ConfigureAwait(false);
    }

    private ICalculationsQueryResultV1 MapToConcreteResultDto(OrchestrationDescriptionUniqueName uniqueName, OrchestrationInstance instance)
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

            default:
                throw new InvalidOperationException($"Unsupported unique name '{uniqueName.Name}'.");
        }
    }
}
