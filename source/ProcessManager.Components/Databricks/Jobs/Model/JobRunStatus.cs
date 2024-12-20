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

namespace Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;

/// <summary>
/// The converted status of a Databricks job execution.
/// The purpose of this is to create an abstraction to the status used in the
/// Databricks Jobs REST API documented here: https://docs.databricks.com/api/azure/workspace/jobs/getrun#status
/// </summary>
public enum JobRunStatus
{
    Pending = 0,
    Queued,
    Running,
    Completed,
    Failed,
    Canceled,
}
