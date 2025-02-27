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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NodaTime;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Api;

internal class SearchOrchestrationInstancesByNameTrigger(
    ILogger<SearchOrchestrationInstancesByNameTrigger> logger,
    IOrchestrationInstanceQueries queries)
{
    private readonly ILogger _logger = logger;
    private readonly IOrchestrationInstanceQueries _queries = queries;

    [Function(nameof(SearchOrchestrationInstancesByNameTrigger))]
    [Authorize]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/query/name")]
        HttpRequest httpRequest,
        [FromBody]
        SearchOrchestrationInstancesByNameQuery query,
        FunctionContext executionContext)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        var lifecycleState = query.LifecycleStates?
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

        var orchestrationInstances = await _queries
            .SearchAsync(
                query.Name,
                query.Version,
                lifecycleState,
                terminationState,
                startedAtOrLater,
                terminatedAtOrEarlier,
                scheduledAtOrLater)
            .ConfigureAwait(false);

        var dto = orchestrationInstances.MapToDto();
        return new OkObjectResult(dto);
    }
}
