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
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ProcessManager.Components.Diagnostics.HealthChecks;

/// <summary>
/// This health check verifies that we can reach the Databricks Jobs API of a workspace.
/// </summary>
internal sealed class DatabricksJobsApiHealthCheck : IHealthCheck
{
    private readonly DatabricksClient _databricksClient;

    internal DatabricksJobsApiHealthCheck(DatabricksClient databricksClient)
    {
        _databricksClient = databricksClient;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _databricksClient.Jobs
                .ListPageable(cancellationToken: cancellationToken)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Databricks Jobs API is unhealthy", ex);
        }
    }
}
