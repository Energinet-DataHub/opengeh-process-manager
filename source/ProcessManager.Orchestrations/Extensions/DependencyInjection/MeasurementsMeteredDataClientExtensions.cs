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
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Components.DataHub.Measurements;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class MeasurementsMeteredDataClientExtensions
{
    /// <summary>
    /// Register Measurements metered data client.
    /// </summary>
    public static IServiceCollection AddMeasurementsMeteredDataClient(this IServiceCollection services)
    {
        services
            .AddOptions<MeasurementsMeteredDataClientOptions>()
            .BindConfiguration(MeasurementsMeteredDataClientOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<EventHubProducerClient, EventHubProducerClientOptions>(
                        (_, _, provider) =>
                        {
                            var options = provider.GetRequiredService<IOptions<MeasurementsMeteredDataClientOptions>>().Value;
                            return new EventHubProducerClient($"{options.NamespaceName}.servicebus.windows.net", options.EventHubName, new DefaultAzureCredential());
                        })
                    .WithName(EventHubProducerClientNames.MeasurementsEventHub);
            });

        services.AddTransient<IMeasurementsMeteredDataClient, MeasurementsMeteredDataClient>();

        services.AddHealthChecks()
            .AddAzureEventHub(
                clientFactory: sp => sp.GetRequiredService<IAzureClientFactory<EventHubProducerClient>>().CreateClient(EventHubProducerClientNames.MeasurementsEventHub),
                name: EventHubProducerClientNames.MeasurementsEventHub,
                HealthStatus.Unhealthy);
        return services;
    }
}
