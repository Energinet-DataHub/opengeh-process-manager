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
using Energinet.DataHub.MarketParticipant.Authorization.Services;
using Energinet.DataHub.ProcessManager.Components.Authorization;
using Energinet.DataHub.ProcessManager.Components.Authorization.Model;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

public static class AuthorizationClientExtensions
{
    public static IServiceCollection AddAuthorizationClient(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthorizationClientOptions>()
            .BindConfiguration(AuthorizationClientOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddHttpClient(
            HttpClientNames.MarkPartAuthorizationClientName,
            (sp, httpClient) =>
            {
                var options = sp.GetRequiredService<IOptions<AuthorizationClientOptions>>().Value;
                httpClient.BaseAddress = new Uri(options.BaseUrl);
            });

        services.TryAddScoped<AuthorizationRequestService>(provider =>
            new AuthorizationRequestService(
                provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(HttpClientNames.MarkPartAuthorizationClientName)));

        services.TryAddScoped<IAuthorizationClient, AuthorizationClient>();

        var baseUrl = configuration
            .GetSection(AuthorizationClientOptions.SectionName)
            .Get<AuthorizationClientOptions>()!.BaseUrl;

        services
            .AddHealthChecks()
            .AddServiceHealthCheck(
                serviceName: "MarketPart Authorization",
                serviceUri: new Uri(baseUrl + "/api/monitor/live"));

        return services;
    }
}
