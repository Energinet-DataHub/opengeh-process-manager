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
using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>
/// </summary>
public static class EventhubExtensions
{
    public static void AddSharedEventHub(this IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddOptions<EventHubOptions>()
            .BindConfiguration(EventHubOptions.SectionName)
            .ValidateDataAnnotations();

        serviceCollection.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EventHubOptions>>().Value;
            return new EventHubProducerClient(options.FullyQualifiedNamespace, options.EventHubName, new DefaultAzureCredential());
        });

        serviceCollection
            .AddHealthChecks()
            .AddAzureEventHub();
    }
}
