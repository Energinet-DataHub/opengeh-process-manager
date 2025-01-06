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

using Energinet.DataHub.Core.App.WebApp.Extensions.Builder;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Fixtures;

public sealed class HealthChecksFixture : IDisposable
{
    private readonly TestServer _server;

    public HealthChecksFixture()
    {
        var integrationTestConfiguration = new IntegrationTestConfiguration();
        var webHostBuilder = CreateWebHostBuilder(integrationTestConfiguration);
        _server = new TestServer(webHostBuilder);

        HttpClient = _server.CreateClient();
    }

    public HttpClient HttpClient { get; }

    public void Dispose()
    {
        _server.Dispose();
    }

    private static IWebHostBuilder CreateWebHostBuilder(IntegrationTestConfiguration integrationTestConfiguration)
    {
        return new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddInMemoryConfiguration(new Dictionary<string, string?>
                {
                    [$"{DatabricksWorkspaceOptions.SectionName}:{nameof(DatabricksWorkspaceOptions.BaseUrl)}"]
                                = integrationTestConfiguration.DatabricksSettings.WorkspaceUrl,
                    [$"{DatabricksWorkspaceOptions.SectionName}:{nameof(DatabricksWorkspaceOptions.Token)}"]
                                = integrationTestConfiguration.DatabricksSettings.WorkspaceAccessToken,
                });

                services.AddRouting();

                // Register Databricks Jobs with health check
                services.AddDatabricksJobs();
            })
            .Configure(app =>
            {
                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    // Databricks Jobs health check is registered for "ready" endpoint
                    endpoints.MapReadyHealthChecks();
                });
            });
    }
}
