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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Energinet.DataHub.ProcessManager.Example.Consumer.Functions.BRS_X03_ActorRequestProcessExample;

/// <summary>
/// Http trigger to start a new BRS X03 orchestration.
/// </summary>
public class StartTrigger_Brs_X03(
    IProcessManagerMessageClient messageClient)
{
    private readonly IProcessManagerMessageClient _messageClient = messageClient;

    [Function(nameof(StartTrigger_Brs_X03))]
    public Task Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "actor-request-process/start")]
        HttpRequest httpRequest,
        [FromBody]
        StartTriggerInput input,
        FunctionContext executionContext)
    {
        var actorNumber = ActorNumber.Create("1234567890123");
        var actorRole = ActorRole.EnergySupplier;
        return _messageClient.StartNewOrchestrationInstanceAsync(
            new StartActorRequestProcessExampleV1(
                operatingIdentity: new ActorIdentityDto(actorNumber, actorRole),
                inputParameter: new ActorRequestProcessExampleInputV1(
                    RequestedByActorNumber: actorNumber.Value,
                    RequestedByActorRole: actorRole.Name,
                    BusinessReason: input.BusinessReason),
                idempotencyKey: input.IdempotencyKey,
                actorMessageId: Guid.NewGuid().ToString(),
                transactionId: Guid.NewGuid().ToString()),
            CancellationToken.None);
    }

    public record StartTriggerInput(
        string IdempotencyKey,
        string BusinessReason);
}
