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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Steps;

internal class CalculationStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceContext orchestrationInstanceContext)
        : StepExecutor(
            context,
            defaultRetryOptions,
            orchestrationInstanceContext.OrchestrationInstanceId)
{
    internal const string StepDescription = "Beregning";
    private const int CalculationStepSequence = 1;

    protected override int StepSequenceNumber => CalculationStepSequence;

    protected override async Task<OrchestrationStepTerminationState> OnExecuteAsync()
    {
        // Start calculation (Databricks)
        var jobRunId = await Context.CallActivityAsync<JobRunId>(
            nameof(CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCal_V1),
            new CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCal_V1.ActivityInput(
                orchestrationInstanceContext.OrchestrationInstanceId),
            DefaultRetryOptions);

        var expiryTime = Context.CurrentUtcDateTime
            .AddSeconds(orchestrationInstanceContext.OrchestrationOptions.CalculationJobStatusExpiryTimeInSeconds);
        while (Context.CurrentUtcDateTime < expiryTime)
        {
            // Monitor calculation (Databricks)
            var jobRunStatus = await Context.CallActivityAsync<JobRunStatus>(
                nameof(CalculationStepGetJobRunStatusActivity_Brs_045_MissingMeasurementsLogCal_V1),
                new CalculationStepGetJobRunStatusActivity_Brs_045_MissingMeasurementsLogCal_V1.ActivityInput(
                    jobRunId),
                DefaultRetryOptions);

            switch (jobRunStatus)
            {
                case JobRunStatus.Pending:
                case JobRunStatus.Queued:
                case JobRunStatus.Running:
                    // Wait for the next checkpoint
                    var nextCheckpoint = Context.CurrentUtcDateTime
                        .AddSeconds(orchestrationInstanceContext.OrchestrationOptions.CalculationJobStatusPollingIntervalInSeconds);
                    await Context.CreateTimer(nextCheckpoint, CancellationToken.None);
                    break;

                case JobRunStatus.Completed:
                    return OrchestrationStepTerminationState.Succeeded;

                case JobRunStatus.Failed:
                case JobRunStatus.Canceled:
                    throw new Exception($"Databricks job with id: {jobRunId} had status {jobRunStatus.ToString()}");

                default:
                    throw new InvalidOperationException($"Unknown job run status '{jobRunStatus.ToString()}' for jobRunId: {jobRunId}.");
            }
        }

        throw new Exception($"Databricks job with id: {jobRunId} did not finish within giving time");
    }
}
