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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Components.Abstractions.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;

public class EnqueueActorMessagesClient(
    IOptions<ProcessManagerOptions> options,
    IAzureClientFactory<ServiceBusSender> serviceBusFactory)
        : IEnqueueActorMessagesClient
{
    private readonly IOptions<ProcessManagerOptions> _options = options;
    private readonly ServiceBusSender _serviceBusSender = serviceBusFactory.CreateClient(ServiceBusSenderNames.EdiTopic);

    public async Task EnqueueAsync<TEnqueueData>(
        OrchestrationDescriptionUniqueNameDto orchestration,
        Guid orchestrationInstanceId,
        IOperatingIdentityDto orchestrationStartedBy,
        Guid idempotencyKey,
        TEnqueueData data)
            where TEnqueueData : IEnqueueDataDto
    {
        var (startedByActorNumber, startedByActorRole, startedByUserId) = orchestrationStartedBy switch
        {
            ActorIdentityDto actor => (actor.ActorNumber, actor.ActorRole, (Guid?)null),
            UserIdentityDto user => (user.ActorNumber, user.ActorRole, user.UserId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(orchestrationStartedBy),
                orchestrationStartedBy.GetType().Name,
                "Unknown enqueuedBy type"),
        };

        var enqueueActorMessages = new EnqueueActorMessagesV1
        {
            OrchestrationName = orchestration.Name,
            OrchestrationVersion = orchestration.Version,
            OrchestrationStartedByActor = new EnqueueActorMessagesActorV1
            {
                ActorNumber = startedByActorNumber.Value,
                ActorRole = startedByActorRole.ToActorRoleV1(),
            },
            OrchestrationInstanceId = orchestrationInstanceId.ToString(),
        };
        enqueueActorMessages.SetData(data);

        if (startedByUserId is not null)
            enqueueActorMessages.OrchestrationStartedByUserId = startedByUserId.ToString();

        var serviceBusMessage = enqueueActorMessages.ToServiceBusMessage(
            subject: EnqueueActorMessagesV1.BuildServiceBusMessageSubject(orchestration.Name),
            idempotencyKey: idempotencyKey.ToString());

        var originalActorMessageId = data switch
        {
            INotifyEnqueueAcceptedDataDto d => d.OriginalActorMessageId,
            INotifyEnqueueRejectedDataDto d => d.OriginalActorMessageId,
            _ => null,
        };

        if (_options.Value.AllowMockDependenciesForTests
            && originalActorMessageId != null
            && originalActorMessageId.IsTestUuid())
        {
            // Do nothing if this is a test message (don't pollute EDI)
        }
        else
        {
            await _serviceBusSender.SendMessageAsync(serviceBusMessage)
                .ConfigureAwait(false);
        }
    }
}
