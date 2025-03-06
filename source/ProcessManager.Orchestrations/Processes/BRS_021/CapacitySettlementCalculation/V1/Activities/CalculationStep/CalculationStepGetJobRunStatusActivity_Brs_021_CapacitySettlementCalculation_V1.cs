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

using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.CapacitySettlementCalculation.V1.Activities.CalculationStep;

internal class CalculationStepGetJobRunStatusActivity_Brs_021_CapacitySettlementCalculation_V1(
    [FromKeyedServices(DatabricksWorkspaceNames.Measurements)] IDatabricksJobsClient client)
{
    [Function(nameof(CalculationStepGetJobRunStatusActivity_Brs_021_CapacitySettlementCalculation_V1))]
    public async Task<JobRunStatus> Run(
        [ActivityTrigger] ActivityInput input)
    {
        return await client.GetJobRunStatusAsync(input.RunId).ConfigureAwait(false);
    }

    public record ActivityInput(
        JobRunId RunId);
}
