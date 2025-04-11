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

using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.CustomQueries.Examples.V1;

internal class SearchTrigger_ExampleById_V1(
    SearchExampleByIdHandlerV1 handler)
{
    private readonly SearchExampleByIdHandlerV1 _handler = handler;

    /// <summary>
    /// Get Example orchestration instance by id.
    /// </summary>
    [Function(nameof(SearchTrigger_ExampleById_V1))]
    [Authorize]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = $"orchestrationinstance/query/custom/{ExampleByIdQueryV1.RouteName}")]
        HttpRequest httpRequest,
        [FromBody]
        ExampleByIdQueryV1 query,
        FunctionContext executionContext)
    {
        var queryResultItem = await _handler.HandleAsync(query).ConfigureAwait(false);

        return new OkObjectResult(new JsonPolymorphicItemContainer<IExamplesQueryResultV1>(queryResultItem));
    }
}
