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

using Energinet.DataHub.Core.Databricks.Jobs.Abstractions;
using Microsoft.Azure.Databricks.Client.Models;

namespace ProcessManager.Components.Databricks.Jobs;

internal class DatabricksJobsManager(
    IJobsApiClient client)
        : IDatabricksJobsManager
{
    private readonly IJobsApiClient _jobsApiClient = client;

    /// <inheritdoc />
    public async Task<JobRunId> StartJobAsync(string jobName, IReadOnlyCollection<string> jobParameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        var job = await FindJobByNameAsync(jobName).ConfigureAwait(false);
        var inputParameters = RunParameters.CreatePythonParams(jobParameters);
        var runId = await StartJobAsync(job.JobId, inputParameters).ConfigureAwait(false);

        return new JobRunId(runId);
    }

    /// <inheritdoc />
    public async Task<RunStatus> GetRunStatusAsync(JobRunId runId)
    {
        var run = await GetRunByIdAsync(runId).ConfigureAwait(false);

        return run.Run.Status;
    }

    private ValueTask<Job> FindJobByNameAsync(string jobName)
    {
        return _jobsApiClient.Jobs
            .ListPageable(name: jobName)
            .SingleAsync();
    }

    private Task<long> StartJobAsync(long jobId, RunParameters inputParameters)
    {
        return _jobsApiClient
            .Jobs
            .RunNow(jobId, inputParameters);
    }

    private Task<(Run Run, RepairHistory RepairHistory)> GetRunByIdAsync(JobRunId runId)
    {
        return _jobsApiClient
            .Jobs
            .RunsGet(runId.Id);
    }
}
