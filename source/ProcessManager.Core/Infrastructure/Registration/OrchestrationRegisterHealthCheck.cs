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

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;

/// <summary>
/// A health check to verify the health of the orchestration register. Returns <see cref="HealthStatus.Unhealthy"/> if
/// an exception occurred during orchestration register synchronization at application startup.
/// </summary>
/// <param name="orchestrationRegisterContext"></param>
public class OrchestrationRegisterHealthCheck(OrchestrationRegisterContext orchestrationRegisterContext) : IHealthCheck
{
    private readonly OrchestrationRegisterContext _orchestrationRegisterContext = orchestrationRegisterContext;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var synchronizeException = _orchestrationRegisterContext.SynchronizeException;
        if (synchronizeException is not null)
        {
            return Task.FromResult(
                new HealthCheckResult(
                    HealthStatus.Healthy));
        }

        var unhealthyResult = new HealthCheckResult(
            status: HealthStatus.Unhealthy,
            description: synchronizeException!.Message,
            exception: synchronizeException);

        return Task.FromResult(unhealthyResult);
    }
}
