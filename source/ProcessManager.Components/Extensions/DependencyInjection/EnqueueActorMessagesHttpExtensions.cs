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

using Azure.Core;
using Energinet.DataHub.ProcessManager.Components.Authorization;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

public static class EnqueueActorMessagesHttpExtensions
{
    public static IServiceCollection AddEnqueueActorMessagesHttp(this IServiceCollection services, TokenCredential credential)
    {
        services
            .AddOptions<EdiEnqueueActorMessagesHttpClientOptions>()
            .BindConfiguration(EdiEnqueueActorMessagesHttpClientOptions.SectionName)
            .ValidateDataAnnotations();

        // Copy and paste from the PM-client.
        // Should be moved to a shared library: #https://app.zenhub.com/workspaces/mosaic-60a6105157304f00119be86e/issues/gh/energinet-datahub/team-mosaic/724
        services.TryAddSingleton<IAuthorizationHeaderProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EdiEnqueueActorMessagesHttpClientOptions>>().Value;
            return new AuthorizationHeaderProvider(credential, options.ApplicationIdUri);
        });

        services.AddHttpClient(
            HttpClientNames.EdiEnqueueActorMessagesClientName,
            (sp, httpClient) =>
            {
                var options = sp.GetRequiredService<IOptions<EdiEnqueueActorMessagesHttpClientOptions>>().Value;
                ConfigureHttpClient(sp, httpClient, options.BaseUrl);
            });

        services.AddScoped<IEnqueueActorMessagesHttpClient, EnqueueActorMessagesHttpClient>();

        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider sp, HttpClient httpClient, string baseAddress)
    {
        httpClient.BaseAddress = new Uri(baseAddress);

        var headerProvider = sp.GetRequiredService<IAuthorizationHeaderProvider>();
        httpClient.DefaultRequestHeaders.Authorization = headerProvider.CreateAuthorizationHeader();
    }
}
