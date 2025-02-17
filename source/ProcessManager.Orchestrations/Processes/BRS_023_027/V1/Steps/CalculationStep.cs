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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.Steps;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "ConfigureAwait must not be used in durable function code")]
internal class CalculationStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceContext orchestrationInstanceContext)
        : StepExecutor(
            context,
            defaultRetryOptions,
            orchestrationInstanceContext.OrchestrationInstanceId)
{
    private const int CalculationStepSequence = 1;

    protected override int StepSequenceNumber => CalculationStepSequence;

    protected override async Task<OrchestrationStepTerminationState> OnExecuteAsync()
    {
        // Start calculation (Databricks)
        var jobRunId = await Context.CallActivityAsync<JobRunId>(
            nameof(CalculationStepStartJobActivity_Brs_023_027_V1),
            new CalculationStepStartJobActivity_Brs_023_027_V1.ActivityInput(
                orchestrationInstanceContext.OrchestrationInstanceId,
                orchestrationInstanceContext.CalculationId,
                orchestrationInstanceContext.UserId),
            DefaultRetryOptions);

        // TODO: We currently have removed the following functionality compared to the orchestration in Wholesale:
        //  - Updating job status in SQL database; it can be found in durable function monitor if we need to
        //  - "Restart" the calculation job if it was canceled; not sure this is a valid feature anymore
        var continueCalculationMonitor = true;
        var expiryTime = Context.CurrentUtcDateTime
            .AddSeconds(orchestrationInstanceContext.OrchestrationOptions.CalculationJobStatusExpiryTimeInSeconds);
        while (continueCalculationMonitor && Context.CurrentUtcDateTime < expiryTime)
        {
            // Monitor calculation (Databricks)
            var jobRunStatus = await Context.CallActivityAsync<JobRunStatus>(
                nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1),
                new CalculationStepGetJobRunStatusActivity_Brs_023_027_V1.ActivityInput(
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
                    throw new ArgumentException(); // TODO: Add proper message

                case JobRunStatus.Canceled:
                    throw new ArgumentException(); // TODO: Add proper message

                default:
                    throw new InvalidOperationException("Unknown job run status '{jobRunStatus}'.");
            }
        }

        throw new ArgumentException(); // TODO: Add proper message
    }
}
