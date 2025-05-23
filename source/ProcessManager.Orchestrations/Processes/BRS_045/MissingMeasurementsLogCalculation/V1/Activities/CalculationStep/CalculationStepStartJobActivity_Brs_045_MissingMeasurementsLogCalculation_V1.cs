﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Components.Time;
using Energinet.DataHub.ProcessManager.Components.WorkingDays;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep;

internal class CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCalculation_V1(
    [FromKeyedServices(DatabricksWorkspaceNames.Measurements)]
    IDatabricksJobsClient client,
    DataHubCalendar dataHubCalendar)
{
    private readonly IDatabricksJobsClient _client = client;

    [Function(nameof(CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCalculation_V1))]
    public async Task<JobRunId> Run(
        [ActivityTrigger] ActivityInput input)
    {
        // The missing measurement logs the calculation period:
        // 3 working days back from today, and 93 days back from today.
        var relativeWorkingDay = dataHubCalendar.GetWorkingDayRelativeToTodayBackInTime(3);
        var periodStart = dataHubCalendar.GetDateRelativeToCurrentDate(-93);
        var periodEnd = relativeWorkingDay;
        var jobParameters = new List<string>
        {
            $"--orchestration-instance-id={input.OrchestrationInstanceId.Value}",
            $"--period-start-datetime={periodStart}",
            $"--period-end-datetime={periodEnd}",
        };

        return await _client.StartJobAsync("MissingMeasurementsLog", jobParameters).ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);
}
