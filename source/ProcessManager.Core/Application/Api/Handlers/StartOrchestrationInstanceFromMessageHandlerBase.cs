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

using Azure.Messaging.ServiceBus;
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

    protected abstract Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        TInputParameterDto input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId);

    private async Task HandleV1(ServiceBusReceivedMessage message)
    {
        var startOrchestration = message.ParseBody<StartOrchestrationInstanceV1>();

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
            idempotencyKey: message.GetIdempotencyKey(),
            actorMessageId: startOrchestration.ActorMessageId,
            transactionId: startOrchestration.TransactionId,
            meteringPointId: startOrchestration.HasMeteringPointId ? startOrchestration.MeteringPointId : null)
            .ConfigureAwait(false);
    }
}
