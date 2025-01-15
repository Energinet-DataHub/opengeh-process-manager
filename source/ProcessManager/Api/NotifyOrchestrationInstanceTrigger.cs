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
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Api;

public class NotifyOrchestrationInstanceTrigger(
    INotifyOrchestrationInstanceCommands notifyOrchestrationCommands)
{
    private readonly INotifyOrchestrationInstanceCommands _notifyOrchestrationCommands = notifyOrchestrationCommands;

    /// <summary>
    /// Start a BRS-026 request.
    /// </summary>
    [Function(nameof(NotifyOrchestrationInstanceTrigger))]
    public Task Run(
        [ServiceBusTrigger(
            $"%{NotifyOrchestrationInstanceOptions.SectionName}:{nameof(NotifyOrchestrationInstanceOptions.TopicName)}%",
            $"%{NotifyOrchestrationInstanceOptions.SectionName}:{nameof(NotifyOrchestrationInstanceOptions.NotifyOrchestrationInstanceSubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        // TODO: Parse correctly, check major version etc.

        string orchestrationInstanceId;
        string eventName;
        object? eventData;

        var majorVersion = message.GetMajorVersion();
        var bodyFormat = message.GetBodyFormat();
        if (majorVersion == NotifyOrchestrationInstanceV1.MajorVersion)
        {
            (orchestrationInstanceId, eventName, eventData) = HandleV1(
                message: message,
                bodyFormat: bodyFormat);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(majorVersion),
                majorVersion,
                $"Unhandled major version in received notify service bus message (MessageId={message.MessageId})");
        }

        return _notifyOrchestrationCommands.NotifyOrchestrationInstanceAsync(
            new OrchestrationInstanceId(Guid.Parse(orchestrationInstanceId)),
            eventName,
            eventData);
    }

    private (string OrchestrationInstanceId, string EventName, object? EventData) HandleV1(
        ServiceBusReceivedMessage message,
        string bodyFormat)
    {
        var notifyOrchestrationInstanceV1 = bodyFormat switch
        {
            "application/json" => NotifyOrchestrationInstanceV1.Parser.ParseJson(message.Body.ToString()),
            "application/octet-stream" => NotifyOrchestrationInstanceV1.Parser.ParseFrom(message.Body),
            _ => throw new ArgumentOutOfRangeException(
                nameof(bodyFormat),
                bodyFormat,
                $"Unhandled body format in received {nameof(NotifyOrchestrationInstanceV1)} message (MessageId={message.MessageId})"),
        };

        var orchestrationInstanceId = notifyOrchestrationInstanceV1.OrchestrationInstanceId;
        var eventName = notifyOrchestrationInstanceV1.EventName;

        object? eventData = null;
        if (notifyOrchestrationInstanceV1.Data != null)
        {
            eventData = notifyOrchestrationInstanceV1.Data.DataFormat switch
            {
                "application/json" => JsonSerializer.Deserialize<object>(notifyOrchestrationInstanceV1.Data.Data),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(notifyOrchestrationInstanceV1.Data.DataFormat),
                    notifyOrchestrationInstanceV1.Data.DataFormat,
                    $"Unhandled data format in received {nameof(NotifyOrchestrationInstanceV1)} message (MessageId={message.MessageId})"),
            };
        }

        return (orchestrationInstanceId, eventName, eventData);
    }
}
