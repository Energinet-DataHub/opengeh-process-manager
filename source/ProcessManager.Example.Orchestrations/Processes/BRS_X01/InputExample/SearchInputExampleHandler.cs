﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample;

internal class SearchInputExampleHandler(
    IOrchestrationInstanceQueries queries) :
        ISearchOrchestrationInstancesQueryHandler<InputExampleQuery, InputExampleQueryResult>
{
    private readonly IOrchestrationInstanceQueries _queries = queries;

    public async Task<IReadOnlyCollection<InputExampleQueryResult>> HandleAsync(InputExampleQuery query)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
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

        return calculations
            .Select(item => new InputExampleQueryResult(item.MapToTypedDto<InputV1>()))
            .ToList();
    }
}
