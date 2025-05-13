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
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Activities.CalculationStep;

internal class CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogOnDemandCalculation_V1(
    IOrchestrationInstanceProgressRepository repository,
    [FromKeyedServices(DatabricksWorkspaceNames.Measurements)] IDatabricksJobsClient client)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IDatabricksJobsClient _client = client;

    [Function(nameof(CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogOnDemandCalculation_V1))]
    public async Task<JobRunId> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<CalculationInputV1>();

        var jobParameters = new List<string>
        {
            $"--orchestration-instance-id={input.OrchestrationInstanceId.Value}",
            $"--period-start-datetime={orchestrationInstanceInput.PeriodStartDate.ToInstant()}",
            $"--period-end-datetime={orchestrationInstanceInput.PeriodEndDate.ToInstant()}",
            $"--grid-area-codes={string.Join(",", orchestrationInstanceInput.GridAreaCodes)}",
        };

        return await _client.StartJobAsync("MissingMeasurementsLogOnDemand", jobParameters).ConfigureAwait(false);
    }

    public record ActivityInput(OrchestrationInstanceId OrchestrationInstanceId);
}
