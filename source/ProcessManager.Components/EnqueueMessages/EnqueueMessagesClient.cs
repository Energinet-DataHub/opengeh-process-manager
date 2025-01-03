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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Google.Protobuf;
using Microsoft.Extensions.Azure;

namespace Energinet.DataHub.ProcessManager.Components.EnqueueMessages;

public class EnqueueMessagesClient(
    IAzureClientFactory<ServiceBusSender> serviceBusFactory)
    : IEnqueueMessagesClient
{
    private readonly ServiceBusSender _serviceBusSender = serviceBusFactory.CreateClient(ServiceBusSenderNames.EdiTopic);

    public async Task Enqueue<TData>(
        OrchestrationDescriptionUniqueNameDto orchestration,
        string messageId,
        TData data,
        string? messageType = null)
    {
        var message = new EnqueueMessagesDto
        {
            OrchestrationName = orchestration.Name,
            OrchestrationVersion = orchestration.Version,
            EnqueuedByActorId = "123", // TODO: Correct actor id
            JsonInput = JsonSerializer.Serialize(data),
        };

        if (messageType is not null)
            message.MessageType = messageType;

        ServiceBusMessage serviceBusMessage = new(JsonFormatter.Default.Format(message))
        {
            Subject = "Enqueue_" + orchestration.Name.ToLower(),
            MessageId = messageId,
            ContentType = "application/json",
        };

        await _serviceBusSender.SendMessageAsync(serviceBusMessage)
            .ConfigureAwait(false);
    }

    public Task EnqueueAccepted<TData>(OrchestrationDescriptionUniqueNameDto orchestration, string messageId, TData data)
    {
        return Enqueue(orchestration, messageId, data, "Accepted");
    }

    public Task EnqueueRejected<TData>(OrchestrationDescriptionUniqueNameDto orchestration, string messageId, TData data)
    {
        return Enqueue(orchestration, messageId, data, "Rejected");
    }
}
