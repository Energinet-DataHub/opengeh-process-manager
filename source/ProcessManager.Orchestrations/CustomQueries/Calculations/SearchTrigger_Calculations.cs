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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations;

internal class SearchTrigger_Calculations(
    SearchCalculationsHandler handler)
{
    private readonly SearchCalculationsHandler _handler = handler;

    /// <summary>
    /// Search for Calculations orchestration instances.
    /// </summary>
    [Function(nameof(SearchTrigger_Calculations))]
    [Authorize]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = $"orchestrationinstance/query/custom/{CalculationsQuery.RouteName}")]
        HttpRequest httpRequest,
        [FromBody]
        CalculationsQuery query,
        FunctionContext executionContext)
    {
        var queryResultItems = await _handler.HandleAsync(query).ConfigureAwait(false);
        return new OkObjectResult(queryResultItems);
    }
}
