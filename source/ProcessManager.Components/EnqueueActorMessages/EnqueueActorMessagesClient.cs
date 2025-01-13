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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using Google.Protobuf;
using Microsoft.Extensions.Azure;

namespace Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;

public class EnqueueActorMessagesClient(
    IAzureClientFactory<ServiceBusSender> serviceBusFactory)
    : IEnqueueActorMessagesClient
{
    private readonly ServiceBusSender _serviceBusSender = serviceBusFactory.CreateClient(ServiceBusSenderNames.EdiTopic);

    public async Task Enqueue<TInputData>(
        OrchestrationDescriptionUniqueNameDto orchestration,
        IOperatingIdentityDto orchestrationStartedBy,
        string messageId,
        TInputData data)
    {
        var (startedByActorId, startedByUserId) = orchestrationStartedBy switch
        {
            ActorIdentityDto actor => (actor.ActorId, (Guid?)null),
            UserIdentityDto user => (user.ActorId, user.UserId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(orchestrationStartedBy),
                orchestrationStartedBy.GetType().Name,
                "Unknown enqueuedBy type"),
        };

        var enqueueActorMessages = new EnqueueActorMessagesV1
        {
            OrchestrationName = orchestration.Name,
            OrchestrationVersion = orchestration.Version,
            OrchestrationStartedByActorId = startedByActorId.ToString(),
            Data = JsonSerializer.Serialize(data),
            DataType = typeof(TInputData).Name,
            DataFormat = "application/json",
        };

        if (startedByUserId is not null)
            enqueueActorMessages.OrchestrationStartedByUserId = startedByUserId.ToString();

        var serviceBusMessage = enqueueActorMessages.ToServiceBusMessage(
            subject: "Enqueue_" + orchestration.Name.ToLower(),
            idempotencyKey: messageId);

        await _serviceBusSender.SendMessageAsync(serviceBusMessage)
            .ConfigureAwait(false);
    }
}
