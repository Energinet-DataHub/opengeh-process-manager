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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027;

internal class SearchTrigger_Brs_023_027(
    SearchCalculationHandler handler)
{
    private readonly SearchCalculationHandler _handler = handler;

    /// <summary>
    /// Search for instances of BRS-023 or BRS-027 calculations.
    /// </summary>
    [Function(nameof(SearchTrigger_Brs_023_027))]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/query/custom/brs_023_027")]
        HttpRequest httpRequest,
        [FromBody]
        CalculationQuery query,
        FunctionContext executionContext)
    {
        var queryReultItems = await _handler.SearchAsync(query).ConfigureAwait(false);

        return new OkObjectResult(queryReultItems);
    }
}
