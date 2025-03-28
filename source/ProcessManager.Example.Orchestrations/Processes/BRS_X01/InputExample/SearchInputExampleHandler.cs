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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;

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

        return calculations
            .Select(item => new InputExampleQueryResult(item.MapToTypedDto<InputV1>()))
            .ToList();
    }
}
