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

using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Mappers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.CalculationStep;

internal class CalculationStepStartJobActivity_Brs_023_027_V1(
    [FromKeyedServices(DatabricksWorkspaceNames.Wholesale)] IDatabricksJobsClient client)
{
    private readonly IDatabricksJobsClient _client = client;

    [Function(nameof(CalculationStepStartJobActivity_Brs_023_027_V1))]
    public async Task<JobRunId> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var gridAreas = string.Join(", ", input.OrchestrationInput.GridAreaCodes);
        var jobParameters = new List<string>
        {
            $"--calculation-id={input.InstanceId.Value}",
            $"--grid-areas=[{gridAreas}]",
            $"--period-start-datetime={input.OrchestrationInput.PeriodStartDate.ToInstant()}",
            $"--period-end-datetime={input.OrchestrationInput.PeriodEndDate.ToInstant()}",
            $"--calculation-type={CalculationTypeMapper.ToDeltaTableValue(input.OrchestrationInput.CalculationType)}",
            $"--created-by-user-id={input.UserId}",
        };
        if (input.OrchestrationInput.IsInternalCalculation)
            jobParameters.Add("--is-internal-calculation");

        return await _client.StartJobAsync("CalculatorJob", jobParameters).ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        Guid UserId,
        CalculationInputV1 OrchestrationInput);
}
