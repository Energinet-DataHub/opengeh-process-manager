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

using Microsoft.Azure.Databricks.Client.Models;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

/// <summary>
/// A collection of extensions methods that provides abstractions on top of
/// the more technical databricks api extensions in <see cref="DatabricksApiWireMockExtensions"/>
/// </summary>
public static class DatabricksAbstractionExtensions
{
    /// <summary>
    /// Setup databrick api response mocks to be able to respond with the job state provided by <paramref name="state"/>
    /// Supports PENDING, RUNNING and TERMINATED (which resolves to SUCCESS resultState)
    /// </summary>
    public static WireMockServer MockCalculationJobStatusResponse(
        this WireMockServer server,
        RunLifeCycleState state,
        string jobName,
        int? runId = null)
    {
        // => Databricks Jobs API
        var jobId = Random.Shared.Next(1, 1000);
        runId ??= Random.Shared.Next(1000, 2000);

        var (jobRunState, resultState) = state switch
        {
            RunLifeCycleState.PENDING => ("PENDING", "CANCELED"), // "CANCELED" should not matter here
            RunLifeCycleState.RUNNING => ("RUNNING", "CANCELED"), // "CANCELED" should not matter here
            RunLifeCycleState.TERMINATED => ("TERMINATED", "SUCCESS"),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The given state is not implemented"),
        };

        // => Mock job run status to respond according to the jobRunState and resultState
        server
            .MockJobsList(jobId, jobName)
            .MockJobsGet(jobId, jobName)
            .MockJobsRunNow(runId.Value)
            .MockJobsRunsGet(runId.Value, jobRunState, resultState, jobName);

        return server;
    }
}
