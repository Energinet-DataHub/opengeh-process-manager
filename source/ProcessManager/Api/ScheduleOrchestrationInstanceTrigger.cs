﻿// Copyright 2020 Energinet DataHub A/S
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

using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NodaTime.Extensions;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Api;

internal class ScheduleOrchestrationInstanceTrigger(
    ILogger<ScheduleOrchestrationInstanceTrigger> logger,
    IStartOrchestrationInstanceCommands manager)
{
    private readonly ILogger _logger = logger;
    private readonly IStartOrchestrationInstanceCommands _manager = manager;

    /// <summary>
    /// Schedule an orchestration instance and return its id.
    /// </summary>
    [Function(nameof(ScheduleOrchestrationInstanceTrigger))]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/command/schedule")]
        HttpRequest httpRequest,
        [FromBody]
        GeneralScheduleOrchestrationInstanceCommand command,
        FunctionContext executionContext)
    {
        var orchestrationInstanceId = await _manager
            .ScheduleNewOrchestrationInstanceAsync(
                identity: new UserIdentity(
                    new UserId(command.OperatingIdentity.UserId),
                    new ActorId(command.OperatingIdentity.ActorId)),
                uniqueName: new OrchestrationDescriptionUniqueName(
                    command.OrchestrationDescriptionUniqueName.Name,
                    command.OrchestrationDescriptionUniqueName.Version),
                runAt: command.RunAt.ToInstant())
            .ConfigureAwait(false);

        return new OkObjectResult(orchestrationInstanceId.Value);
    }
}