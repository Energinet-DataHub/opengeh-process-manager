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
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Shared.Extensions;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

internal class StartOrchestrationInstanceFromMessageHandler : IStartOrchestrationInstanceFromMessageHandler
{
    public StartOrchestrationInstanceFromMessageHandler(IReadOnlyCollection<IStartOrchestrationInstanceHandler> startOrchestrationInstanceHandlers)
    {
        StartOrchestrationInstanceHandlers = startOrchestrationInstanceHandlers;
    }

    public IReadOnlyCollection<IStartOrchestrationInstanceHandler> StartOrchestrationInstanceHandlers { get; }

    public async Task HandleAsync(ServiceBusReceivedMessage message)
    {
        var majorVersion = message.GetMajorVersion();

        if (majorVersion == StartOrchestrationInstanceV1.MajorVersion)
        {
            await HandleV1Async(message).ConfigureAwait(false);
        }

        throw new ArgumentOutOfRangeException(
                nameof(majorVersion),
                majorVersion,
                $"Unhandled major version in the received start orchestration service bus message (Subject={message.Subject}, MessageId={message.MessageId}).");
    }

    private async Task HandleV1Async(ServiceBusReceivedMessage message)
    {
        var startOrchestration = message.ParseBody<StartOrchestrationInstanceV1>();
        var handler = StartOrchestrationInstanceHandlers.First(x => x.CanHandle(startOrchestration));
        await handler.HandleAsync(startOrchestration, new IdempotencyKey(message.GetIdempotencyKey())).ConfigureAwait(false);
    }
}
