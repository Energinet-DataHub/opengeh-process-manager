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
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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

        var lifecycleStates = query.LifecycleStates.MapToDomain();
        var terminationState = query.TerminationState.MapToDomain();

        var scheduledAtOrLater = query.ScheduledAtOrLater.ToNullableInstant();
        var startedAtOrLater = query.StartedAtOrLater.ToNullableInstant();
        var terminatedAtOrEarlier = query.TerminatedAtOrEarlier.ToNullableInstant();

        var orchestrationInstances = await _queries
            .SearchAsync(
                query.Name,
                query.Version,
                lifecycleStates,
                terminationState,
                startedAtOrLater,
                terminatedAtOrEarlier,
                scheduledAtOrLater)
            .ConfigureAwait(false);

        var dto = orchestrationInstances.MapToDto();
        return new OkObjectResult(dto);
    }
}
