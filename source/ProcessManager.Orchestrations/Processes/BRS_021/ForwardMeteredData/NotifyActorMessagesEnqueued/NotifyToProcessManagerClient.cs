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
using Google.Protobuf;
using Microsoft.Extensions.Azure;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.NotifyActorMessagesEnqueued;

public class NotifyToProcessManagerClient(
    IAzureClientFactory<ServiceBusSender> serviceBusFactory)
    : INotifyToProcessManagerClient
{
    public const string ForwardMeteredDataServiceBusName = "NotifyBrs21Topic";
    private readonly ServiceBusSender _serviceBusSender = serviceBusFactory.CreateClient(ForwardMeteredDataServiceBusName);

    public async Task Notify(NotifyOrchestrationInstanceEvent notifyEvent)
    {
        var serviceBusMessage = CreateNotifyOrchestrationInstanceServiceBusMessage(
            notifyEvent);

        await _serviceBusSender.SendMessageAsync(serviceBusMessage)
            .ConfigureAwait(false);
    }

    private ServiceBusMessage CreateNotifyOrchestrationInstanceServiceBusMessage(
        NotifyOrchestrationInstanceEvent notifyEvent)
    {
        var notifyOrchestration = new NotifyOrchestrationInstanceV1
        {
            OrchestrationInstanceId = notifyEvent.OrchestrationInstanceId,
            EventName = notifyEvent.EventName,
        };

        var serviceBusMessage = ToServiceBusMessage(
            notifyOrchestration,
            subject: "hej",
            idempotencyKey: "123");

        return serviceBusMessage;
    }

    private ServiceBusMessage ToServiceBusMessage(IMessage message, string subject, string idempotencyKey)
    {
        ServiceBusMessage serviceBusMessage = new(JsonFormatter.Default.Format(message))
        {
            Subject = subject,
            MessageId = idempotencyKey,
            ContentType = "application/json",
        };
        string majorVersionKey = "MajorVersion";
        string bodyFormatKey = "BodyFormat";

        serviceBusMessage.ApplicationProperties.Add(majorVersionKey, message.GetType().Name);
        serviceBusMessage.ApplicationProperties.Add(bodyFormatKey, "Json");

        return serviceBusMessage;
    }
}
