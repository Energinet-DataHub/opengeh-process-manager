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
using Energinet.DataHub.ProcessManager.Shared.Extensions;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

public class MagicHandlerFactory : IMagicHandlerFactory
{
    private readonly IReadOnlyCollection<IMagicHandler> _magicHandlers;

    public MagicHandlerFactory(IReadOnlyCollection<IMagicHandler> magicHandlers)
    {
        _magicHandlers = magicHandlers;
    }

    public IMagicHandler Create(ServiceBusReceivedMessage message)
    {
        var majorVersion = message.GetMajorVersion();

        if (majorVersion == StartOrchestrationInstanceV1.MajorVersion)
        {
            return HandleV1(message);
        }

        throw new ArgumentOutOfRangeException(
                nameof(majorVersion),
                majorVersion,
                $"Unhandled major version in the received start orchestration service bus message (Subject={message.Subject}, MessageId={message.MessageId}).");
    }

    private IMagicHandler HandleV1(ServiceBusReceivedMessage message)
    {
        var startOrchestration = message.ParseBody<StartOrchestrationInstanceV1>();
        return _magicHandlers.First(x => x.CanHandle(startOrchestration));
    }
}
