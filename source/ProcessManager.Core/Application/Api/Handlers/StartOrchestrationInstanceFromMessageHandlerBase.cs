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
using DurableTask.Core.Common;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

public abstract class StartOrchestrationInstanceFromMessageHandlerBase<TInputParameterDto>(
    ILogger logger)
    where TInputParameterDto : class, IInputParameterDto
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
                message.ApplicationProperties,
            },
        });

        _logger.LogInformation("Handling received start orchestration service bus message.");

        var majorVersion = message.GetMajorVersion();
        if (majorVersion == StartOrchestrationInstanceV1.MajorVersion)
        {
            await HandleV1(message)
                .ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(majorVersion),
                majorVersion,
                $"Unhandled major version in the received start orchestration service bus message (Subject={message.Subject}, MessageId={message.MessageId}).");
        }
    }

    protected abstract Task StartOrchestrationInstanceAsync(ActorIdentity actorIdentity, TInputParameterDto input, string idempotencyKey);

    private async Task HandleV1(ServiceBusReceivedMessage message)
    {
        var startOrchestration = message.ParseMessageBody<StartOrchestrationInstanceV1>();
        // var startOrchestration = messageBodyFormat switch
        // {
        //     ServiceBusMessageBodyFormat.Json => StartOrchestrationInstanceV1.Parser.ParseJson(message.Body.ToString()),
        //     ServiceBusMessageBodyFormat.Binary => StartOrchestrationInstanceV1.Parser.ParseFrom(message.Body),
        //     _ => throw new ArgumentOutOfRangeException(
        //         nameof(messageBodyFormat),
        //         messageBodyFormat,
        //         $"Unhandled message body format when deserializing the received {nameof(StartOrchestrationInstanceV1)} message (MessageId={message.MessageId}, Subject={message.Subject})"),
        // };

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
                startOrchestration.InputFormat,
            },
        });

        var inputParameterDto = startOrchestration.ParseInput<TInputParameterDto>();

        if (!Guid.TryParse(startOrchestration.StartedByActorId, out var actorId))
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(StartOrchestrationInstanceV1.StartedByActorId),
                actualValue: startOrchestration.StartedByActorId,
                message: $"Unable to parse {nameof(startOrchestration.StartedByActorId)} to guid (MessageId={message.MessageId}, Subject={message.Subject})");
        }

        await StartOrchestrationInstanceAsync(
            new ActorIdentity(new ActorId(actorId)),
            inputParameterDto,
            idempotencyKey: message.MessageId)
            .ConfigureAwait(false);
    }
}
