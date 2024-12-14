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
using Microsoft.Azure.Databricks.Client.Models;
using ProcessManager.Components.Databricks.Jobs.Model;

namespace ProcessManager.Components.Databricks.Jobs;

internal class DatabricksJobsClient(
    IJobsApi jobsApi)
        : IDatabricksJobsClient
{
    private readonly IJobsApi _jobsApi = jobsApi;

    /// <inheritdoc />
    public async Task<JobRunId> StartJobAsync(string jobName, IReadOnlyCollection<string> pythonParameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        var jobId = await FindJobIdByNameAsync(jobName).ConfigureAwait(false);
        var runParameters = RunParameters.CreatePythonParams(pythonParameters);
        var runId = await StartJobAsync(jobId, runParameters).ConfigureAwait(false);

        return new JobRunId(runId);
    }

    /// <inheritdoc />
    public async Task<JobRunStatus> GetRunStatusAsync(JobRunId runId)
    {
        var result = await GetRunByIdAsync(runId).ConfigureAwait(false);

        return ConvertToJobRunStatus(result.Run);
    }

    /// <summary>
    /// Convert from the status used by the Databricks Jobs REST API documented
    /// here: https://docs.databricks.com/api/azure/workspace/jobs/getrun#status
    /// </summary>
    private static JobRunStatus ConvertToJobRunStatus(Run jobRun)
    {
        return jobRun.Status.State switch
        {
            RunStatusState.PENDING => JobRunStatus.Pending,
            RunStatusState.QUEUED => JobRunStatus.Queued,
            RunStatusState.RUNNING => JobRunStatus.Running,

            RunStatusState.TERMINATED or
            RunStatusState.TERMINATING => jobRun.Status.TerminationDetails.Code switch
            {
                RunTerminationCode.SUCCESS => JobRunStatus.Completed,

                RunTerminationCode.USER_CANCELED or
                RunTerminationCode.CANCELED => JobRunStatus.Canceled,

                _ => JobRunStatus.Failed,
            },

            _ => JobRunStatus.Failed,
        };
    }

    private async Task<long> FindJobIdByNameAsync(string jobName)
    {
        var job = await _jobsApi
            .ListPageable(name: jobName)
            .SingleAsync()
            .ConfigureAwait(false);

        return job.JobId;
    }

    private Task<long> StartJobAsync(long jobId, RunParameters inputParameters)
    {
        return _jobsApi
            .RunNow(jobId, inputParameters);
    }

    private Task<(Run Run, RepairHistory RepairHistory)> GetRunByIdAsync(JobRunId runId)
    {
        return _jobsApi
            .RunsGet(runId.Id);
    }
}
