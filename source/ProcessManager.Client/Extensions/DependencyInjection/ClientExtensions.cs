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
using Azure.Messaging.ServiceBus;
using Energinet.DataHub.ProcessManager.Abstractions.Client;
using Energinet.DataHub.ProcessManager.Client.Authorization;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// If <see cref="IHttpContextAccessor"/> is registered we try to retrieve the "Authorization"
    /// header value and forward it to the Process Manager API for authentication/authorization.
    /// </summary>
    public static IServiceCollection AddProcessManagerHttpClients(this IServiceCollection services)
    {
        services
            .AddOptions<ProcessManagerHttpClientsOptions>()
            .BindConfiguration(ProcessManagerHttpClientsOptions.SectionName)
            .ValidateDataAnnotations();

        services.TryAddSingleton<IAuthorizationHeaderProvider>(sp =>
        {
            // We currently register AuthorizationHeaderProvider like this to be in control of the
            // creation of DefaultAzureCredential.
            // As we register IAuthorizationHeaderProvider as singleton and it has the instance
            // of DefaultAzureCredential, we expect it will use caching and handle token refresh.
            // However the documentation is a bit unclear: https://learn.microsoft.com/da-dk/dotnet/azure/sdk/authentication/best-practices?tabs=aspdotnet#understand-when-token-lifetime-and-caching-logic-is-needed
            var credential = new DefaultAzureCredential();
            var options = sp.GetRequiredService<IOptions<ProcessManagerHttpClientsOptions>>().Value;
            return new AuthorizationHeaderProvider(credential, options.ApplicationIdUri);
        });

        services.AddHttpClient(HttpClientNames.GeneralApi, (sp, httpClient) =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessManagerHttpClientsOptions>>().Value;
            ConfigureHttpClient(sp, httpClient, options.GeneralApiBaseAddress);
        });
        services.AddHttpClient(HttpClientNames.OrchestrationsApi, (sp, httpClient) =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessManagerHttpClientsOptions>>().Value;
            ConfigureHttpClient(sp, httpClient, options.OrchestrationsApiBaseAddress);
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
                            .CreateSender(serviceBusOptions.Brs021ForwardMeasurementsStartTopicName);
                    })
                    .WithName(StartSenderClientNames.Brs021ForwardMeasurementsStartSender);

                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var serviceBusOptions = provider.GetRequiredService<IOptions<ProcessManagerServiceBusClientOptions>>().Value;
                        return provider
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(serviceBusOptions.Brs021ForwardMeasurementsNotifyTopicName);
                    })
                    .WithName(NotifySenderClientNames.Brs021ForwardMeasurementsNotifySender);
            });

        services.AddScoped<IProcessManagerMessageClient, ProcessManagerMessageClient>();

        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider sp, HttpClient httpClient, string baseAddress)
    {
        httpClient.BaseAddress = new Uri(baseAddress);

        var headerProvider = sp.GetRequiredService<IAuthorizationHeaderProvider>();
        httpClient.DefaultRequestHeaders.Authorization = headerProvider.CreateAuthorizationHeader();
    }
}
