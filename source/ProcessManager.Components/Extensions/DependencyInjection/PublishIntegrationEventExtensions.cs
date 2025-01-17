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

using Azure.Core;
using Azure.Messaging.ServiceBus;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

public static class PublishIntegrationEventExtensions
{
        public static IServiceCollection AddIntegrationEventPublisher(this IServiceCollection services, TokenCredential azureCredential)
    {
        services.AddOptions<ServiceBusNamespaceOptions>()
            .BindConfiguration(ServiceBusNamespaceOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddOptions<IntegrationEventTopicOptions>()
            .BindConfiguration(IntegrationEventTopicOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddHealthChecks()
            .AddAzureServiceBusTopic(
                sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                sp => sp.GetRequiredService<IOptions<IntegrationEventTopicOptions>>().Value.Name,
                tokenCredentialFactory: _ => azureCredential,
                name: "Shared topic");

        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                        (_, _, provider) =>
                        {
                            var serviceBusOptions = provider.GetRequiredService<IOptions<IntegrationEventTopicOptions>>().Value;
                            var serviceBusSender = provider
                                .GetRequiredService<ServiceBusClient>()
                                .CreateSender(serviceBusOptions.Name);

                            return serviceBusSender;
                        })
                    .WithName(ServiceBusSenderNames.IntegrationEventTopic);
            });

        // TODO: Hent noget andet
        services.AddTransient<IEnqueueActorMessagesClient, EnqueueActorMessagesClient>();

        return services;
    }
}
