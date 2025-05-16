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

using Energinet.DataHub.Core.Databricks.SqlStatementExecution;
using Energinet.DataHub.Core.Databricks.SqlStatementExecution.Diagnostics.HealthChecks;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs;
using Energinet.DataHub.ProcessManager.Components.Extensions.Builder;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Azure.Databricks.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>
/// that allow adding Databricks REST API clients.
/// </summary>
public static class DatabricksWorkspaceExtensions
{
    /// <summary>
    /// Register Databricks Jobs API services, options and health check for use with a single Databricks workspace.
    /// Options are read from the default configuration section name.
    /// </summary>
    /// <remarks>
    /// If your application needs to access multiple Databricks workspaces, use the overloaded
    /// method <see cref="AddDatabricksJobsApi(IServiceCollection, string)"/> that allows you to specify
    /// the configuration section name.
    /// </remarks>
    public static IServiceCollection AddDatabricksJobsApi(this IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddOptions<DatabricksWorkspaceOptions>()
            .BindConfiguration(DatabricksWorkspaceOptions.SectionName)
            .ValidateDataAnnotations();

        serviceCollection.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabricksWorkspaceOptions>>().Value;
            return DatabricksClient.CreateClient(options.BaseUrl, options.Token);
        });

        serviceCollection.AddTransient<IDatabricksJobsClient>(sp =>
        {
            var databricksClient = sp.GetRequiredService<DatabricksClient>();
            return new DatabricksJobsClient(databricksClient.Jobs);
        });

        serviceCollection
            .AddHealthChecks()
            .AddDatabricksJobsApi(name: "Databricks Jobs API");

        return serviceCollection;
    }

    /// <summary>
    /// Register Databricks Jobs API services, options and health check for use with a Databricks workspace.
    /// </summary>
    /// <remarks>
    /// By using different <paramref name="configSectionPath"/> it is possible to register
    /// services for multiple Databricks workspaces in the same application.
    /// Services are registered using Keyed services (https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#keyed-services).
    /// The "key" used is the value given in <paramref name="configSectionPath"/>.
    /// </remarks>
    /// <param name="serviceCollection"></param>
    /// <param name="configSectionPath">Name of the config section from which we read options.</param>
    public static IServiceCollection AddDatabricksJobsApi(this IServiceCollection serviceCollection, string configSectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

        serviceCollection
            .AddOptions<DatabricksWorkspaceOptions>(name: configSectionPath)
            .BindConfiguration(configSectionPath)
            .ValidateDataAnnotations();

        serviceCollection.AddKeyedSingleton(serviceKey: configSectionPath, (sp, key) =>
        {
            using (var serviceScope = sp.CreateScope())
            {
                var snapshot = serviceScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DatabricksWorkspaceOptions>>();
                var options = snapshot.Get(configSectionPath);
                return DatabricksClient.CreateClient(options.BaseUrl, options.Token);
            }
        });

        serviceCollection.AddKeyedTransient<IDatabricksJobsClient>(serviceKey: configSectionPath, (sp, key) =>
        {
            var databricksClient = sp.GetRequiredKeyedService<DatabricksClient>(key);
            return new DatabricksJobsClient(databricksClient.Jobs);
        });

        serviceCollection
            .AddHealthChecks()
            .AddDatabricksJobsApi(
                serviceKey: configSectionPath,
                name: $"{configSectionPath}: Databricks Jobs API");

        return serviceCollection;
    }

    /// <summary>
    /// Register Databricks SQL statement API services, options and health check for use with a Databricks workspace.
    /// <remarks>Requires <see cref="DatabricksSqlStatementOptions"/> settings to exist in the app configuration.</remarks>
    /// </summary>
    public static IServiceCollection AddDatabricksSqlStatementApi(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection
            .AddDatabricksSqlStatementExecution(configuration, TokenProvider.ServicePrincipalTokenProvider);

        serviceCollection
            .AddHealthChecks()
            .AddDatabricksSqlStatementApiHealthCheck(name: "Databricks SQL statement API");

        return serviceCollection;
    }
}
