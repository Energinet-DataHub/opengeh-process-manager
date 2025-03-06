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
    /// Register Process Manager default message client for use in applications.
    /// Options are read from the default configuration section name.
    /// <remarks>The application must register the <see cref="ServiceBusClient"/> and contain configuration for <see cref="ProcessManagerMessageClientOptions"/></remarks>
    /// </summary>
    public static IServiceCollection AddProcessManagerMessageClient(this IServiceCollection services)
    {
        services
            .AddOptions<ProcessManagerMessageClientOptions>()
            .BindConfiguration(ProcessManagerMessageClientOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, sp) =>
                    {
                        var options = sp.GetRequiredService<IOptions<ProcessManagerMessageClientOptions>>().Value;
                        return sp
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(options.StartTopicName);
                    })
                    .WithName($"{ProcessManagerMessageClientOptions.SectionName}{ServiceBusSenderNameSuffix.StartSender}");

                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, sp) =>
                    {
                        var options = sp.GetRequiredService<IOptions<ProcessManagerMessageClientOptions>>().Value;
                        return sp
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(options.NotifyTopicName);
                    })
                    .WithName($"{ProcessManagerMessageClientOptions.SectionName}{ServiceBusSenderNameSuffix.NotifySender}");
            });

        services.AddTransient<IProcessManagerMessageClient>(sp =>
        {
            var senderClientFactory = sp.GetRequiredService<IAzureClientFactory<ServiceBusSender>>();
            var startSender = senderClientFactory.CreateClient($"{ProcessManagerMessageClientOptions.SectionName}{ServiceBusSenderNameSuffix.StartSender}");
            var notifySender = senderClientFactory.CreateClient($"{ProcessManagerMessageClientOptions.SectionName}{ServiceBusSenderNameSuffix.NotifySender}");

            return new ProcessManagerMessageClient(startSender, notifySender);
        });

        return services;
    }

    /// <summary>
    /// Register Process Manager special message client <see cref="MessageClientNames"/> for use in applications.
    /// Options are read from the <paramref name="configSectionPath"/> configuration section name.
    /// <remarks>
    /// The application must register the <see cref="ServiceBusClient"/> and contain configuration for <see cref="ProcessManagerMessageClientOptions"/>
    ///
    /// By using different <paramref name="configSectionPath"/> it is possible to register
    /// services for multiple special clients in the same application.
    /// Services are registered using Keyed services (https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#keyed-services).
    /// The "key" used is the value given in <paramref name="configSectionPath"/>.
    /// </remarks>
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configSectionPath">Name of the config section from which we read options.</param>
    public static IServiceCollection AddProcessManagerMessageClient(this IServiceCollection services, string configSectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

        services
            .AddOptions<ProcessManagerMessageClientOptions>(name: configSectionPath)
            .BindConfiguration(configSectionPath)
            .ValidateDataAnnotations();

        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var snapshot = provider.GetRequiredService<IOptionsSnapshot<ProcessManagerMessageClientOptions>>();
                        var options = snapshot.Get(configSectionPath);
                        return provider
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(options.StartTopicName);
                    })
                    .WithName($"{configSectionPath}{ServiceBusSenderNameSuffix.StartSender}");

                builder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
                    (_, _, provider) =>
                    {
                        var snapshot = provider.GetRequiredService<IOptionsSnapshot<ProcessManagerMessageClientOptions>>();
                        var options = snapshot.Get(configSectionPath);
                        return provider
                            .GetRequiredService<ServiceBusClient>()
                            .CreateSender(options.NotifyTopicName);
                    })
                    .WithName($"{configSectionPath}{ServiceBusSenderNameSuffix.NotifySender}");
            });

        services.AddKeyedTransient<IProcessManagerMessageClient>(serviceKey: configSectionPath, (sp, key) =>
        {
            var senderClientFactory = sp.GetRequiredService<IAzureClientFactory<ServiceBusSender>>();
            var startSender = senderClientFactory.CreateClient($"{key}{ServiceBusSenderNameSuffix.StartSender}");
            var notifySender = senderClientFactory.CreateClient($"{key}{ServiceBusSenderNameSuffix.NotifySender}");

            return new ProcessManagerMessageClient(startSender, notifySender);
        });

        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider sp, HttpClient httpClient, string baseAddress)
    {
        httpClient.BaseAddress = new Uri(baseAddress);

        var headerProvider = sp.GetRequiredService<IAuthorizationHeaderProvider>();
        httpClient.DefaultRequestHeaders.Authorization = headerProvider.CreateAuthorizationHeader();
    }
}
