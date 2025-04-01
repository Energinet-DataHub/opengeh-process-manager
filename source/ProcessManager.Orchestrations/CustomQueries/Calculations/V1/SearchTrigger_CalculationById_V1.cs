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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;

internal class SearchTrigger_CalculationById_V1(
    SearchCalculationByIdHandlerV1 handler)
{
    private readonly SearchCalculationByIdHandlerV1 _handler = handler;

    /// <summary>
    /// Get Calculation orchestration instance by id.
    /// </summary>
    [Function(nameof(SearchTrigger_CalculationById_V1))]
    [Authorize]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = $"orchestrationinstance/query/custom/{CalculationByIdQueryV1.RouteName}")]
        HttpRequest httpRequest,
        [FromBody]
        CalculationByIdQueryV1 query,
        FunctionContext executionContext)
    {
        var queryResultItem = await _handler.HandleAsync(query).ConfigureAwait(false);

        // Default serialization using 'OkObjectResult' doesn't perform Json Polymorphic correct if we
        // use the type directly; so we use a list as a container.
        var results = new List<ICalculationsQueryResultV1>();
        if (queryResultItem != null)
            results.Add(queryResultItem);

        return new OkObjectResult(results);
    }
}
