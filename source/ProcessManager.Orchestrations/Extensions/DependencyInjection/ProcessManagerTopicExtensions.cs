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

using Azure.Identity;
using Energinet.DataHub.Core.App.Common.Diagnostics.HealthChecks;
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
    public static IServiceCollection AddProcessManagerTopic(this IServiceCollection services, DefaultAzureCredential credential)
    {
        services.AddOptions<ServiceBusNamespaceOptions>()
            .BindConfiguration(ServiceBusNamespaceOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddOptions<ProcessManagerTopicOptions>()
            .BindConfiguration(ProcessManagerTopicOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddHealthChecks()
            .AddAzureServiceBusTopic(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerTopicOptions>>().Value.TopicName,
                tokenCredentialFactory: _ => credential,
                name: "Process Manager Topic")
            .AddAzureServiceBusSubscription(
                fullyQualifiedNamespaceFactory: sp => sp.GetRequiredService<IOptions<ServiceBusNamespaceOptions>>().Value.FullyQualifiedNamespace,
                topicNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerTopicOptions>>().Value.TopicName,
                subscriptionNameFactory: sp => sp.GetRequiredService<IOptions<ProcessManagerTopicOptions>>().Value.Brs026SubscriptionName,
                tokenCredentialFactory: _ => credential,
                name: "BRS-026 Subscription");

        return services;
    }
}
