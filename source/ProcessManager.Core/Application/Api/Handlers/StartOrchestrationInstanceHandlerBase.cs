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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

public abstract class StartOrchestrationInstanceHandlerBase<TInputParameterDto>(
    ILogger logger) : IStartOrchestrationInstanceHandler
    where TInputParameterDto : class, IInputParameterDto
{
    protected ILogger Logger => logger;

    public abstract bool CanHandle(StartOrchestrationInstanceV1 startOrchestrationInstance);

    public async Task HandleAsync(StartOrchestrationInstanceV1 startOrchestrationInstance, IdempotencyKey idempotencyKey)
    {
        using var startOrchestrationLoggerScope = Logger.BeginScope(new
        {
            StartOrchestration = new
            {
                startOrchestrationInstance.OrchestrationName,
                startOrchestrationInstance.OrchestrationVersion,
                OperatingIdentity = new
                {
                    Actor = startOrchestrationInstance.StartedByActor,
                },
                startOrchestrationInstance.InputFormat,
                startOrchestrationInstance.ActorMessageId,
                startOrchestrationInstance.TransactionId,
                startOrchestrationInstance.MeteringPointId,
            },
        });

        var inputParameterDto = startOrchestrationInstance.ParseInput<TInputParameterDto>();

        await StartOrchestrationInstanceAsync(
                actorIdentity: new ActorIdentity(
                    Actor.From(
                        startOrchestrationInstance.StartedByActor.ActorNumber,
                        startOrchestrationInstance.StartedByActor.ActorRole)),
                input: inputParameterDto,
                idempotencyKey: idempotencyKey.Value,
                actorMessageId: startOrchestrationInstance.ActorMessageId,
                transactionId: startOrchestrationInstance.TransactionId,
                meteringPointId: startOrchestrationInstance.HasMeteringPointId ? startOrchestrationInstance.MeteringPointId : null)
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
