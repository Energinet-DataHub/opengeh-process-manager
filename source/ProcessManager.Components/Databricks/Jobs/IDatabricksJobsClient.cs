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

using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;

namespace Energinet.DataHub.ProcessManager.Components.Databricks.Jobs;

public interface IDatabricksJobsClient
{
    /// <summary>
    /// Find the Databricks job by name and start it using given
    /// python parameters.
    /// </summary>
    /// <param name="jobName">Name of the job in the Databricks workspace.</param>
    /// <param name="pythonParameters">Python parameters that will be given as input to the job when started.</param>
    /// <returns>Run id.</returns>
    Task<JobRunId> StartJobAsync(string jobName, IReadOnlyCollection<string> pythonParameters);

    /// <summary>
    /// Retrieve the job run status of a given run.
    /// </summary>
    /// <param name="runId">Run id.</param>
    /// <returns>Job run status.</returns>
    Task<JobRunStatus> GetJobRunStatusAsync(JobRunId runId);
}
