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
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Api;

internal class GetOrchestrationInstanceByIdTrigger(
    ILogger<GetOrchestrationInstanceByIdTrigger> logger,
    IOrchestrationInstanceQueries queries)
{
    private readonly ILogger _logger = logger;
    private readonly IOrchestrationInstanceQueries _queries = queries;

    /// <summary>
    /// Get orchestration instance by id.
    /// </summary>
    [Function(nameof(GetOrchestrationInstanceByIdTrigger))]
    [Authorize]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/query/id")]
        HttpRequest httpRequest,
        [FromBody]
        GetOrchestrationInstanceByIdQuery query,
        FunctionContext executionContext)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *

        var orchestrationInstance = await _queries
            .GetAsync(new OrchestrationInstanceId(query.Id))
            .ConfigureAwait(false);

        var dto = orchestrationInstance.MapToDto();
        return new OkObjectResult(dto);
    }
}
