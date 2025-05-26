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
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.App.Common.Identity;
using Energinet.DataHub.ProcessManager.Abstractions.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>
/// that allow adding Process Manager clients to an application.
/// </summary>
public static class ClientExtensions
{
    /// <summary>
    /// Register Process Manager HTTP clients for use in applications.
    /// Configures http clients with an "Authorization" header value and
    /// forward it to the Process Manager API for authentication/authorization.
    /// </summary>
    /// <remarks>
    /// Expects "AddTokenCredentialProvider" has been called to register <see cref="TokenCredentialProvider"/>.
    /// </remarks>
    public static IServiceCollection AddProcessManagerHttpClients(this IServiceCollection services)
    {
        services
            .AddOptions<ProcessManagerHttpClientsOptions>()
            .BindConfiguration(ProcessManagerHttpClientsOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddAuthorizationHeaderProvider();

        services.AddHttpClient(HttpClientNames.GeneralApi, (sp, httpClient) =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessManagerHttpClientsOptions>>().Value;
            ConfigureHttpClient(sp, httpClient, options.GeneralApiBaseAddress, options.ApplicationIdUri);
        });
        services.AddHttpClient(HttpClientNames.OrchestrationsApi, (sp, httpClient) =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessManagerHttpClientsOptions>>().Value;
            ConfigureHttpClient(sp, httpClient, options.OrchestrationsApiBaseAddress, options.ApplicationIdUri);
        });

        services.AddScoped<IProcessManagerClient, ProcessManagerClient>();

        return services;
    }

    /// <summary>
    /// Register Process Manager message client for use in applications.
    /// <remarks>The application must register the <see cref="ServiceBusClient"/> and contain configuration for <see cref="ProcessManagerServiceBusClientOptions"/></remarks>
    /// </summary>
    public static IServiceCollection AddProcessManagerMessageClient(this IServiceCollection services)
    {
        services
            .AddOptions<ProcessManagerServiceBusClientOptions>()
            .BindConfiguration(ProcessManagerServiceBusClientOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var serviceBusOptions = provider.GetRequiredService<IOptions<ProcessManagerServiceBusClientOptions>>().Value;
                        return provider
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(serviceBusOptions.StartTopicName);
                    })
                    .WithName(StartSenderClientNames.ProcessManagerStartSender);

                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var serviceBusOptions = provider.GetRequiredService<IOptions<ProcessManagerServiceBusClientOptions>>().Value;
                        return provider
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(serviceBusOptions.NotifyTopicName);
                    })
                    .WithName(NotifySenderClientNames.ProcessManagerNotifySender);

                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var serviceBusOptions = provider.GetRequiredService<IOptions<ProcessManagerServiceBusClientOptions>>().Value;
                        return provider
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(serviceBusOptions.Brs021ForwardMeteredDataStartTopicName);
                    })
                    .WithName(StartSenderClientNames.Brs021ForwardMeteredDataStartSender);

                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var serviceBusOptions = provider.GetRequiredService<IOptions<ProcessManagerServiceBusClientOptions>>().Value;
                        return provider
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(serviceBusOptions.Brs021ForwardMeteredDataNotifyTopicName);
                    })
                    .WithName(NotifySenderClientNames.Brs021ForwardMeteredDataNotifySender);
            });

        services.AddScoped<IProcessManagerMessageClient, ProcessManagerMessageClient>();

        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider sp, HttpClient httpClient, string baseAddress, string applicationIdUri)
    {
        httpClient.BaseAddress = new Uri(baseAddress);

        var headerProvider = sp.GetRequiredService<IAuthorizationHeaderProvider>();
        httpClient.DefaultRequestHeaders.Authorization = headerProvider.CreateAuthorizationHeader(applicationIdUri);
    }
}
