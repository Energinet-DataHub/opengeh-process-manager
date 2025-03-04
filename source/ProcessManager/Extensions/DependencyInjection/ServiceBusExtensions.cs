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
using Energinet.DataHub.ProcessManager.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Extensions.DependencyInjection;

public static class ServiceBusExtensions
{
    /// <summary>
    /// Add required services for handling notify orchestration instance Service Bus messages.
    /// <remarks>
    /// Requires <see cref="NotifyOrchestrationInstanceOptions"/> and <see cref="NotifyOrchestrationInstanceOptionsV2"/>
    /// to be present in the app settings.
    /// Requires a <see cref="ServiceBusClient"/> to already be registered in the service collection.
    /// </remarks>
    /// </summary>
    public static IServiceCollection AddNotifyOrchestrationInstance(this IServiceCollection serviceCollection, TokenCredential azureCredential)
    {
        serviceCollection
            .AddOptions<ServiceBusNamespaceOptions>()
            .BindConfiguration(ServiceBusNamespaceOptions.SectionName)
            .ValidateDataAnnotations();

        serviceCollection
            .AddOptions<NotifyOrchestrationInstanceOptions>()
            .BindConfiguration(NotifyOrchestrationInstanceOptions.SectionName)
            .ValidateDataAnnotations();

        serviceCollection
            .AddOptions<NotifyOrchestrationInstanceOptionsV2>()
            .BindConfiguration(NotifyOrchestrationInstanceOptionsV2.SectionName)
            .ValidateDataAnnotations();

        serviceCollection
            .AddHealthChecks()
            .AddAzureServiceBusTopic(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<NotifyOrchestrationInstanceOptions>>().Value.TopicName,
                tokenCredentialFactory: _ => azureCredential,
                name: "Process Manager Topic V1")
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<NotifyOrchestrationInstanceOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<NotifyOrchestrationInstanceOptions>>().Value.NotifyOrchestrationInstanceSubscriptionName,
                tokenCredentialFactory: _ => azureCredential,
                name: "NotifyOrchestrationInstance Subscription V1")
            .AddAzureServiceBusTopic(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<NotifyOrchestrationInstanceOptionsV2>>().Value.TopicName,
                tokenCredentialFactory: _ => azureCredential,
                name: "Process Manager Topic")
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<NotifyOrchestrationInstanceOptionsV2>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<NotifyOrchestrationInstanceOptionsV2>>().Value.SubscriptionName,
                tokenCredentialFactory: _ => azureCredential,
                name: "NotifyOrchestrationInstance Subscription");

        return serviceCollection;
    }
}
