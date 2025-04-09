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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.CustomQueries.Calculations.V1;

internal class SearchExamplesHandlerV1(
    ProcessManagerReaderContext readerContext) :
        ISearchOrchestrationInstancesQueryHandler<ExamplesQueryV1, IExamplesQueryResultV1>
{
    private readonly ProcessManagerReaderContext _readerContext = readerContext;

    public async Task<IReadOnlyCollection<IExamplesQueryResultV1>> HandleAsync(ExamplesQueryV1 query)
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
            .Select(item => ExamplesQueryResultMapperV1.MapToDto(item.UniqueName, item.Instance))
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
    private async Task<IReadOnlyCollection<(OrchestrationDescriptionUniqueName UniqueName, OrchestrationInstance Instance)>>
        SearchAsync(
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
}
