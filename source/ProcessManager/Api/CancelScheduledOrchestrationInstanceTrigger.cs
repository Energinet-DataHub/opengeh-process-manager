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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Api;

internal class CancelScheduledOrchestrationInstanceTrigger(
    ILogger<CancelScheduledOrchestrationInstanceTrigger> logger,
    ICancelScheduledOrchestrationInstanceCommand command)
{
    private readonly ILogger _logger = logger;
    private readonly ICancelScheduledOrchestrationInstanceCommand _command = command;

    /// <summary>
    /// Cancel a scheduled orchestration instance
    /// </summary>
    [Function(nameof(CancelScheduledOrchestrationInstanceTrigger))]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/command/cancel")]
        HttpRequest httpRequest,
        [FromBody]
        CancelScheduledOrchestrationInstanceCommand command,
        FunctionContext executionContext)
    {
        await _command
            .CancelScheduledOrchestrationInstanceAsync(
                new UserIdentity(
                    new UserId(command.OperatingIdentity.UserId),
                    new Actor(command.OperatingIdentity.ActorNumber, command.OperatingIdentity.ActorRole)),
                new OrchestrationInstanceId(command.Id))
            .ConfigureAwait(false);

        return new OkResult();
    }
}
