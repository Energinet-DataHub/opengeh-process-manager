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

using Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Diagnostics.HealthChecks;

/// <summary>
/// A health check to verify the health of the orchestration register. Returns "FailureStatus" if
/// an exception occurred during orchestration register synchronization at application startup.
/// </summary>
/// <param name="orchestrationRegisterContext"></param>
public class OrchestrationRegisterHealthCheck(
    OrchestrationRegisterContext orchestrationRegisterContext)
        : IHealthCheck
{
    private readonly OrchestrationRegisterContext _orchestrationRegisterContext = orchestrationRegisterContext;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var synchronizeException = _orchestrationRegisterContext.SynchronizeException;

            var healthCheckData = new Dictionary<string, object>
            {
                {
                    nameof(_orchestrationRegisterContext.SynchronizedAt),
                    _orchestrationRegisterContext.SynchronizedAt.ToString()
                },
            };

            var healthCheckResult = synchronizeException is null
                ? HealthCheckResult.Healthy(data: healthCheckData)
                : new HealthCheckResult(
                    status: context.Registration.FailureStatus,
                    description: $"Exception during orchestration register synchronization: {synchronizeException.Message}",
                    exception: synchronizeException,
                    data: healthCheckData);

            return Task.FromResult(healthCheckResult);
        }
        catch (Exception e)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, exception: e));
        }
    }
}
