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
using Energinet.DataHub.ProcessManager.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Extensions.DependencyInjection;

public static class ServiceBusExtensions
{
    /// <summary>
    /// Add required services for handling notify orchestration instance Service Bus messages.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProcessManagerNotifyTopicOptions"/> to be present in the app settings.
    /// Requires <see cref="ServiceBusNamespaceOptions"/> to be present in the app settings.
    /// Expects "AddTokenCredentialProvider" has been called to register <see cref="TokenCredentialProvider"/>.
    /// </remarks>
    public static IServiceCollection AddNotifyOrchestrationInstance(this IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddOptions<ServiceBusNamespaceOptions>()
            .BindConfiguration(ServiceBusNamespaceOptions.SectionName)
            .ValidateDataAnnotations();

        serviceCollection
            .AddOptions<ProcessManagerNotifyTopicOptions>()
            .BindConfiguration(ProcessManagerNotifyTopicOptions.SectionName)
            .ValidateDataAnnotations();

        serviceCollection
            .AddHealthChecks()
            .AddAzureServiceBusTopic(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerNotifyTopicOptions>>().Value.TopicName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "Process Manager Notify Topic")
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerNotifyTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerNotifyTopicOptions>>().Value.SubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "NotifyOrchestrationInstance Subscription")
            .AddServiceBusTopicSubscriptionDeadLetter(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerNotifyTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerNotifyTopicOptions>>().Value.SubscriptionName,
                tokenCredentialFactory: sp => sp.GetRequiredService<TokenCredentialProvider>().Credential,
                name: "NotifyOrchestrationInstance Dead-letter",
                tags: [HealthChecksConstants.StatusHealthCheckTag]);

        return serviceCollection;
    }
}
