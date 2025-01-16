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
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;

namespace Energinet.DataHub.ProcessManager.Components.IntegrationEventPublisher;

public class IntegrationEventPublisherClient(
    IAzureClientFactory<ServiceBusSender> serviceBusFactory) 
        : IIntegrationEventPublisherClient
{
    private readonly ServiceBusSender _serviceBusSender = serviceBusFactory.CreateClient(ServiceBusSenderNames.IntegrationEventTopic);

    public async Task PublishAsync<TData>(
        string idempotencyKey,
        TData data,
        CancellationToken cancellationToken)
    {
        var serviceBusMessage = new ServiceBusMessage()
        {
           
        };
        await _serviceBusSender.SendMessageAsync(serviceBusMessage, cancellationToken)
            .ConfigureAwait(false);
    }
}
