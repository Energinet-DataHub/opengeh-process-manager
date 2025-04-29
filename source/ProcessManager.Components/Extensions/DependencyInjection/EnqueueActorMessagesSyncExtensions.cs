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
using Energinet.DataHub.ProcessManager.Components.Authorization;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

public static class EnqueueActorMessagesSyncExtensions
{
    public static IServiceCollection AddEnqueueActorMessagesSync(this IServiceCollection services)
    {
        services
            .AddOptions<EdiEnqueueActorMessageSyncClientOptions>()
            .BindConfiguration(EdiEnqueueActorMessageSyncClientOptions.SectionName)
            .ValidateDataAnnotations();

        services.TryAddSingleton<IAuthorizationHeaderProvider>(sp =>
        {
            // We currently register AuthorizationHeaderProvider like this to be in control of the
            // creation of DefaultAzureCredential.
            // As we register IAuthorizationHeaderProvider as singleton and it has the instance
            // of DefaultAzureCredential, we expect it will use caching and handle token refresh.
            // However the documentation is a bit unclear: https://learn.microsoft.com/da-dk/dotnet/azure/sdk/authentication/best-practices?tabs=aspdotnet#understand-when-token-lifetime-and-caching-logic-is-needed
            var credential = new DefaultAzureCredential();
            var options = sp.GetRequiredService<IOptions<EdiEnqueueActorMessageSyncClientOptions>>().Value;
            return new AuthorizationHeaderProvider(credential, options.ApplicationIdUri);
        });

        services.AddHttpClient(
            EnqueueActorMessagesSyncClient.EdiEnqueueActorMessageSyncClientName,
            (sp, httpClient) =>
            {
                var options = sp.GetRequiredService<IOptions<EdiEnqueueActorMessageSyncClientOptions>>().Value;
                ConfigureHttpClient(sp, httpClient, options.Url);
            });

        services.TryAddSingleton<IEnqueueActorMessagesSyncClient, EnqueueActorMessagesSyncClient>();

        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider sp, HttpClient httpClient, string baseAddress)
    {
        // TODO: Add authentication?
        httpClient.BaseAddress = new Uri(baseAddress);

        var headerProvider = sp.GetRequiredService<IAuthorizationHeaderProvider>();
        httpClient.DefaultRequestHeaders.Authorization = headerProvider.CreateAuthorizationHeader();
    }
}
