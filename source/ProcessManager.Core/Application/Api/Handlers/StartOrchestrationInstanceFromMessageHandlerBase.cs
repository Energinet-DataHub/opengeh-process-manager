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
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

public abstract class StartOrchestrationInstanceFromMessageHandlerBase<TInputParameterDto>(
    ILogger logger)
    where TInputParameterDto : IInputParameterDto
{
    private readonly ILogger _logger = logger;

    public async Task HandleAsync(ServiceBusReceivedMessage message)
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

        var actualMajorVersion = message.GetMajorVersion();
        if (actualMajorVersion == StartOrchestrationV1.MajorVersion)
        {
            await HandleV1(message)
                .ConfigureAwait(false);
        }
        else
        {
            _logger.LogError($"");
            throw new ArgumentOutOfRangeException(
                nameof(actualMajorVersion),
                actualMajorVersion,
                $"Unhandled {nameof(StartOrchestrationV1)} service bus message version.");
        }
    }

    protected abstract Task StartOrchestrationInstanceAsync(ActorIdentity actorIdentity, TInputParameterDto input);

    private async Task HandleV1(ServiceBusReceivedMessage message)
    {
        var startOrchestration = StartOrchestrationV1.Parser.ParseJson(message.Body.ToString());
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
            _logger.LogError($"Unable to deserialize {nameof(startOrchestration.JsonInput)} to {inputTypeName} type:{Environment.NewLine}{0}", startOrchestration.JsonInput);
            throw new ArgumentException($"Unable to deserialize {nameof(startOrchestration.JsonInput)} to {inputTypeName} type");
        }

        if (!Guid.TryParse(startOrchestration.StartedByActorId, out var actorId))
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(StartOrchestrationV1.StartedByActorId),
                actualValue: startOrchestration.StartedByActorId,
                message: $"Unable to parse {nameof(startOrchestration.StartedByActorId)} to guid");
        }

        await StartOrchestrationInstanceAsync(
            new ActorIdentity(new ActorId(actorId)),
            inputParameterDto)
            .ConfigureAwait(false);
    }
}
