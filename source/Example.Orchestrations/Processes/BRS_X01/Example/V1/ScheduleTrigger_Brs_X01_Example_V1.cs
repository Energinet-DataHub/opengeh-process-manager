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

using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_X01.Example.V1.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.Example.Orchestrations.Processes.BRS_X01.Example.V1;

internal class ScheduleTrigger_Brs_X01_Example_V1(
    StartExampleHandlerV1 handler)
{
    private readonly StartExampleHandlerV1 _handler = handler;

    /// <summary>
    /// Schedule a BRS-X01 and return its id.
    /// </summary>
    [Function(nameof(ScheduleTrigger_Brs_X01_Example_V1))]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/command/schedule/custom/brs_x01_example/1")]
        HttpRequest httpRequest,
        [FromBody]
        ScheduleExampleCommandV1 command,
        FunctionContext executionContext)
    {
        var orchestrationInstanceId = await _handler.ScheduleNewExampleAsync(command).ConfigureAwait(false);
        return new OkObjectResult(orchestrationInstanceId.Value);
    }
}
