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
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;

namespace Energinet.DataHub.ProcessManager.Api;

public class NotifyOrchestrationInstanceTriggerV2(
    INotifyOrchestrationInstanceCommands notifyOrchestrationCommands)
{
    private readonly INotifyOrchestrationInstanceCommands _notifyOrchestrationCommands = notifyOrchestrationCommands;

    [Function(nameof(NotifyOrchestrationInstanceTriggerV2))]
    public Task Run(
        [ServiceBusTrigger(
            $"%{ProcessManagerNotifyTopicOptions.SectionName}:{nameof(ProcessManagerNotifyTopicOptions.TopicName)}%",
            $"%{ProcessManagerNotifyTopicOptions.SectionName}:{nameof(ProcessManagerNotifyTopicOptions.SubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        var majorVersion = message.GetMajorVersion();
        var (orchestrationInstanceId, eventName, eventData) = majorVersion switch
        {
            NotifyOrchestrationInstanceV1.MajorVersion => HandleV1(message),
            _ => throw new ArgumentOutOfRangeException(
                nameof(majorVersion),
                majorVersion,
                $"Unhandled major version in received notify service bus message (MessageId={message.MessageId})"),
        };

        return _notifyOrchestrationCommands.NotifyOrchestrationInstanceAsync(
            new OrchestrationInstanceId(Guid.Parse(orchestrationInstanceId)),
            eventName,
            eventData);
    }

    private (string OrchestrationInstanceId, string EventName, object? EventData) HandleV1(ServiceBusReceivedMessage message)
    {
        var notifyOrchestrationInstanceV1 = message.ParseBody<NotifyOrchestrationInstanceV1>();

        var orchestrationInstanceId = notifyOrchestrationInstanceV1.OrchestrationInstanceId;
        var eventName = notifyOrchestrationInstanceV1.EventName;

        // Durable function uses Newtonsoft.Json to serialize the object, so we must deserialize
        // the notify data using Newtonsoft.Json as well, else serialization/deserialization of ExpandoObject doesn't work
        object? eventData = null;
        if (notifyOrchestrationInstanceV1.Data is not null)
            eventData = JsonConvert.DeserializeObject(notifyOrchestrationInstanceV1.Data.Data);

        return (orchestrationInstanceId, eventName, eventData);
    }
}
