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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026;

// TODO:
// Should be moved to another namespace because this is shared between BRS 026 + 028.
// We have talked about combining these BRS's into ne top-folder similar to BRS 023 + 027,
// and then use subfolders to split them per orchestration OR perhaps even use the same orchestration
// because their logic is very similar.
internal class SearchTrigger_Brs_026_028(
    SearchActorRequestHandler handler)
{
    private readonly SearchActorRequestHandler _handler = handler;

    /// <summary>
    /// Search for instances of BRS-026 or BRS-028 calculations.
    /// </summary>
    [Function(nameof(SearchTrigger_Brs_026_028))]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = $"orchestrationinstance/query/custom/{ActorRequestQuery.RouteName}")]
        HttpRequest httpRequest,
        [FromBody]
        ActorRequestQuery query,
        FunctionContext executionContext)
    {
        var queryResultItems = await _handler.HandleAsync(query).ConfigureAwait(false);
        return new OkObjectResult(queryResultItems);
    }
}
