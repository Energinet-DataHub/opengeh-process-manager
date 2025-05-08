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
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

public abstract class StartOrchestrationInstanceHandlerBase<TInputParameterDto>(
    ILogger logger) : IStartOrchestrationInstanceHandler
    where TInputParameterDto : class, IInputParameterDto
{
    private readonly ILogger _logger = logger;

    public abstract bool CanHandle(StartOrchestrationInstanceV1 startOrchestration);

    public async Task HandleAsync(StartOrchestrationInstanceV1 startOrchestration, IdempotencyKey idempotencyKey)
    {
        using var startOrchestrationLoggerScope = _logger.BeginScope(new
        {
            StartOrchestration = new
            {
                startOrchestration.OrchestrationName,
                startOrchestration.OrchestrationVersion,
                OperatingIdentity = new
                {
                    Actor = startOrchestration.StartedByActor,
                },
                startOrchestration.InputFormat,
                startOrchestration.ActorMessageId,
                startOrchestration.TransactionId,
                startOrchestration.MeteringPointId,
            },
        });

        var inputParameterDto = startOrchestration.ParseInput<TInputParameterDto>();

        await StartOrchestrationInstanceAsync(
                actorIdentity: new ActorIdentity(
                    Actor.From(
                        startOrchestration.StartedByActor.ActorNumber,
                        startOrchestration.StartedByActor.ActorRole)),
                input: inputParameterDto,
                idempotencyKey: idempotencyKey.Value,
                actorMessageId: startOrchestration.ActorMessageId,
                transactionId: startOrchestration.TransactionId,
                meteringPointId: startOrchestration.HasMeteringPointId ? startOrchestration.MeteringPointId : null)
            .ConfigureAwait(false);
    }

    protected abstract Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        TInputParameterDto input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId);
}
