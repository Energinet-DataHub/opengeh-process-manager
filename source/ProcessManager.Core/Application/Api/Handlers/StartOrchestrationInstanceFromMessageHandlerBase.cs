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

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

public abstract class StartOrchestrationInstanceFromMessageHandlerBase<TInputParameterDto>(
    ILogger logger)
    where TInputParameterDto : IInputParameterDto
{
    private readonly ILogger _logger = logger;

    public Task HandleAsync(ServiceBusReceivedMessage message)
    {
        using var serviceBusMessageLoggerScope = _logger.BeginScope(new
        {
            ServiceBusMessage = new
            {
                message.MessageId,
                message.CorrelationId,
                message.Subject,
            },
        });

        var jsonMessage = message.Body.ToString();
        var startOrchestration = StartOrchestration.Parser.ParseJson(jsonMessage);
        using var startOrchestrationLoggerScope = _logger.BeginScope(new
        {
            StartOrchestration = new
            {
                startOrchestration.OrchestrationName,
                startOrchestration.OrchestrationVersion,
                OperatingIdentity = new
                {
                    ActorId = startOrchestration.StartedByActorId,
                },
            },
        });

        var inputParameterDto = JsonSerializer.Deserialize<TInputParameterDto>(startOrchestration.JsonInput);
        if (inputParameterDto is null)
        {
            var inputTypeName = typeof(TInputParameterDto).Name;
            _logger.LogWarning($"Unable to deserialize {nameof(startOrchestration.JsonInput)} to {inputTypeName} type:{Environment.NewLine}{0}", startOrchestration.JsonInput);
            throw new ArgumentException($"Unable to deserialize {nameof(startOrchestration.JsonInput)} to {inputTypeName} type");
        }

        if (!Guid.TryParse(startOrchestration.StartedByActorId, out var actorId))
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(StartOrchestration.StartedByActorId),
                actualValue: startOrchestration.StartedByActorId,
                message: $"Unable to parse {nameof(startOrchestration.StartedByActorId)} to guid");
        }

        return StartOrchestrationInstanceAsync(
            new ActorIdentity(new ActorId(actorId)),
            inputParameterDto,
            message.MessageId);
    }

    protected abstract Task StartOrchestrationInstanceAsync(ActorIdentity actorIdentity, TInputParameterDto input, string idempotencyKey);
}
