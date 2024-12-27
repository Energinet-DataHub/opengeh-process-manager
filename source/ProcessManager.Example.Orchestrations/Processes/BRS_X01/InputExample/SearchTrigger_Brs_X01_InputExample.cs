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

using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample;

internal class SearchTrigger_Brs_X01_InputExample(
    SearchInputExampleHandler handler)
{
    private readonly SearchInputExampleHandler _handler = handler;

    /// <summary>
    /// Search for instances of BRS-X01.
    /// </summary>
    [Function(nameof(SearchTrigger_Brs_X01_InputExample))]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/query/custom/brs_x01_inputExample")]
        HttpRequest httpRequest,
        [FromBody]
        InputExampleQuery query,
        FunctionContext executionContext)
    {
        var queryResultItems = await _handler.HandleAsync(query).ConfigureAwait(false);
        return new OkObjectResult(queryResultItems);
    }
}
