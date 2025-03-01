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
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.NotifyActorMessagesEnqueued;

public static class NotifyToProcessManagerExtension
{
    public static IServiceCollection AddEnqueueActorMessageToProcessManager(this IServiceCollection services, TokenCredential azureCredential)
    {
        services.AddOptions<ServiceBusNamespaceOptions>()
            .BindConfiguration(ServiceBusNamespaceOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddOptions<Brs021ForwardMeteredDataTopicOptions>()
            .BindConfiguration(Brs021ForwardMeteredDataTopicOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddHealthChecks()
            .AddAzureServiceBusTopic(
                sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.NotifyTopicName,
                tokenCredentialFactory: _ => azureCredential,
                name: "Notify brs 21 topic");

        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var integrationEventTopicOptions = provider.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value;
                        var serviceBusSender = provider
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(integrationEventTopicOptions.NotifyTopicName);

                        return serviceBusSender;
                    })
                    .WithName(NotifyToProcessManagerClient.ForwardMeteredDataServiceBusName);
            });

        services.AddTransient<INotifyToProcessManagerClient, NotifyToProcessManagerClient>();

        return services;
    }
}
