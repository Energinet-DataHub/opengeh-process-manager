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

using Energinet.DataHub.Core.App.Common.Extensions.Builder;
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.App.Common.Identity;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

public static class EnqueueActorMessagesHttpExtensions
{
    /// <summary>
    /// Register services and health checks for enqueue actor messages over http.
    /// </summary>
    /// <remarks>
    /// Expects "AddTokenCredentialProvider" has been called to register <see cref="TokenCredentialProvider"/>.
    /// </remarks>
    public static IServiceCollection AddEnqueueActorMessagesHttp(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<EnqueueActorMessagesHttpClientOptions>()
            .BindConfiguration(EnqueueActorMessagesHttpClientOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddAuthorizationHeaderProvider();

        services.AddHttpClient(
            HttpClientNames.EdiEnqueueActorMessagesClientName,
            (sp, httpClient) =>
            {
                var options = sp.GetRequiredService<IOptions<EnqueueActorMessagesHttpClientOptions>>().Value;
                ConfigureHttpClient(sp, httpClient, options.BaseUrl, options.ApplicationIdUri);
            });

        services.AddScoped<IEnqueueActorMessagesHttpClient, EnqueueActorMessagesHttpClient>();

        var baseUrl = configuration
            .GetSection(EnqueueActorMessagesHttpClientOptions.SectionName)
            .Get<EnqueueActorMessagesHttpClientOptions>()!.BaseUrl;

        services
            .AddHealthChecks()
            .AddServiceHealthCheck(
                serviceName: "EDI",
                serviceUri: new Uri(baseUrl + "/api/monitor/live"));

        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider sp, HttpClient httpClient, string baseAddress, string applicationIdUri)
    {
        httpClient.BaseAddress = new Uri(baseAddress);

        var headerProvider = sp.GetRequiredService<IAuthorizationHeaderProvider>();
        httpClient.DefaultRequestHeaders.Authorization = headerProvider.CreateAuthorizationHeader(applicationIdUri);
    }
}
