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

using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1;

internal class StartTrigger_Brs_X01_InputExample_V1(
    StartInputExampleHandlerV1 handler)
{
    private readonly StartInputExampleHandlerV1 _handler = handler;

    /// <summary>
    /// Start a BRS-X01 and return its id.
    /// </summary>
    [Function(nameof(StartTrigger_Brs_X01_InputExample_V1))]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/command/start/custom/Brs_X01_InputExample/1")]
        HttpRequest httpRequest,
        [FromBody]
        StartInputExampleCommandV1 command,
        FunctionContext executionContext)
    {
        var orchestrationInstanceId = await _handler.StartNewExampleAsync(command).ConfigureAwait(false);
        return new OkObjectResult(orchestrationInstanceId.Value);
    }
}
