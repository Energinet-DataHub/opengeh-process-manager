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

using Microsoft.Azure.Databricks.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProcessManager.Components.Diagnostics.HealthChecks;
using ProcessManager.Components.Extensions.DependencyInjection;

namespace ProcessManager.Components.Extensions.Builder;

/// <summary>
/// Health check builder extensions to be used from <see cref="DatabricksWorkspaceExtensions"/>.
/// </summary>
internal static class DatabricksHealthCheckBuilderExtensions
{
    /// <summary>
    /// Add a health check that verifies we can reach the Databricks Jobs API of a workspace.
    /// The health check is intended to be used by the "ready" endpoint.
    /// </summary>
    /// <remarks>
    /// If your application needs to access multiple Databricks workspaces, use the overloaded
    /// method <see cref="DatabricksHealthCheckBuilderExtensions.AddDatabricksJobsApi(IHealthChecksBuilder, string, string, IEnumerable{string}?)"/>
    /// that allows you to specify the "service key".
    /// </remarks>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The name of the health check.</param>
    /// <param name="tags">Tags that can be used to filter health checks.</param>
    public static IHealthChecksBuilder AddDatabricksJobsApi(
        this IHealthChecksBuilder builder,
        string name,
        IEnumerable<string>? tags = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return builder.Add(
            new HealthCheckRegistration(
                name,
                sp =>
                {
                    var databricksClient = sp.GetRequiredService<DatabricksClient>();
                    return new DatabricksJobsApiHealthCheck(databricksClient);
                },
                HealthStatus.Unhealthy,
                tags,
                timeout: default));
    }

    /// <summary>
    /// Add a health check that verifies we can reach the Databricks Jobs API of a workspace.
    /// The health check is intended to be used by the "ready" endpoint.
    /// </summary>
    /// <remarks>
    /// Can be used when multiple Databricks workspaces are registered; just specify the "key"
    /// of the "keyed service" in <paramref name="serviceKey"/>.
    /// </remarks>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="serviceKey">The "key" used for retrieving the Databricks client registered as a "keyed service".</param>
    /// <param name="name">The name of the health check.</param>
    /// <param name="tags">Tags that can be used to filter health checks.</param>
    public static IHealthChecksBuilder AddDatabricksJobsApi(
        this IHealthChecksBuilder builder,
        string serviceKey,
        string name,
        IEnumerable<string>? tags = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return builder.Add(
            new HealthCheckRegistration(
                name,
                sp =>
                {
                    var databricksClient = sp.GetRequiredKeyedService<DatabricksClient>(serviceKey);
                    return new DatabricksJobsApiHealthCheck(databricksClient);
                },
                HealthStatus.Unhealthy,
                tags,
                timeout: default));
    }
}
