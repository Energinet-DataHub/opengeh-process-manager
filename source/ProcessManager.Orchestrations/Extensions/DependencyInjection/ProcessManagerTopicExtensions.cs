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

using Energinet.DataHub.Core.App.Common.Diagnostics.HealthChecks;
using Energinet.DataHub.Core.App.Common.Identity;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Builder;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;

public static class ProcessManagerTopicExtensions
{
    /// <summary>
    /// Add required dependencies to use the Process Manager Service Bus topic.
    /// </summary>
    /// <remarks>
    /// Expects "AddTokenCredentialProvider" has been called to register <see cref="TokenCredentialProvider"/>.
    /// </remarks>
    public static IServiceCollection AddProcessManagerTopic(this IServiceCollection services)
    {
        services.AddOptions<ServiceBusNamespaceOptions>()
            .BindConfiguration(ServiceBusNamespaceOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddOptions<ProcessManagerStartTopicOptions>()
            .BindConfiguration(ProcessManagerStartTopicOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddOptions<Brs021ForwardMeteredDataTopicOptions>()
            .BindConfiguration(Brs021ForwardMeteredDataTopicOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddHealthChecks()
            .AddAzureServiceBusTopic(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.TopicName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "Process Manager Start Topic")
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.Brs024SubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-024 Subscription")
            .AddServiceBusTopicSubscriptionDeadLetter(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.Brs024SubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-024 Dead-letter",
                [HealthChecksConstants.StatusHealthCheckTag])
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.Brs026SubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-026 Subscription")
            .AddServiceBusTopicSubscriptionDeadLetter(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.Brs026SubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-026 Dead-letter",
                [HealthChecksConstants.StatusHealthCheckTag])
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.Brs028SubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-028 Subscription")
            .AddServiceBusTopicSubscriptionDeadLetter(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerStartTopicOptions>>().Value.Brs028SubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-028 Dead-letter",
                [HealthChecksConstants.StatusHealthCheckTag])
            // Add health check for the Brs021ForwardMeteredData start Topic and subscription
            .AddAzureServiceBusTopic(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.StartTopicName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-021-ForwardMeteredData Start Topic")
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.StartTopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.StartSubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-021-ForwardMeteredData Start Subscription")
            .AddServiceBusTopicSubscriptionDeadLetter(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.StartTopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.StartSubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-021-ForwardMeteredData Start Dead-letter",
                [HealthChecksConstants.StatusHealthCheckTag])
            // Add health check for the Brs021ForwardMeteredData notify Topic and subscription
            .AddAzureServiceBusTopic(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.NotifyTopicName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-021-ForwardMeteredData Notify Topic")
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.NotifyTopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.NotifySubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-021-ForwardMeteredData Notify Subscription")
            .AddServiceBusTopicSubscriptionDeadLetter(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.NotifyTopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<Brs021ForwardMeteredDataTopicOptions>>().Value.NotifySubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "BRS-021-ForwardMeteredData Notify Dead-letter",
                [HealthChecksConstants.StatusHealthCheckTag]);

        return services;
    }
}
