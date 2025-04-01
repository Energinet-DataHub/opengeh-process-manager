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
using Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.FeatureFlags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;

public static class HostBuilderContextExtensions
{
    public static void AddConfiguration(this HostBuilderContext context, IConfigurationBuilder configuration)
    {
        // This code gets the Azure App Configuration endpoint and, if valid, adds it as a configuration source,
        // connecting with DefaultAzureCredential and setting feature flags to refresh every 5 seconds.
        // Feature flags are connected to the application through Azure App Configuration.
        //var appConfigEndpoint = context.Configuration["AppConfigEndpoint"];
        //var appConfigEndpoint = Environment.GetEnvironmentVariable("AppConfigEndpoint");

        var settings = configuration.Build();
        var appConfigEndpoint = settings["AppConfigEndpoint"]!;

        if (!string.IsNullOrEmpty(appConfigEndpoint))
        {
            configuration.AddAzureAppConfiguration(options =>
            {
                options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())
                    .UseFeatureFlags(featureFlagOptions =>
                    {
                        featureFlagOptions.SetRefreshInterval(TimeSpan.FromSeconds(5));
                    });
            });
        }
    }
}
