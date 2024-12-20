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
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1;

// TODO: Implement according to guidelines: https://energinet.atlassian.net/wiki/spaces/D3/pages/824803345/Durable+Functions+Development+Guidelines
internal class Orchestration_Brs_021_ElectricalHeatingCalculation_V1
{
    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_021_ElectricalHeatingCalculation_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    internal static StepIdentifierDto[] Steps => [CalculationStep, EnqueueMessagesStep];

    internal static StepIdentifierDto CalculationStep => new(1, "Beregning");

    internal static StepIdentifierDto EnqueueMessagesStep => new(2, "Besked dannelse");

    [Function(nameof(Orchestration_Brs_021_ElectricalHeatingCalculation_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        // Initialize
        var executionContext = await context.CallActivityAsync<OrchestrationExecutionContext>(
            nameof(OrchestrationInitializeActivity_Brs_021_ElectricalHeatingCalculation_V1),
            new OrchestrationInitializeActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        // Step: Calculation
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_Brs_021_ElectricalHeatingCalculation_V1),
            new TransitionStepToRunningActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                instanceId,
                CalculationStep.Sequence),
            _defaultRetryOptions);

        // Start calculation (Databricks)
        var jobRunId = await context.CallActivityAsync<JobRunId>(
            nameof(CalculationStepStartJobActivity_Brs_021_ElectricalHeatingCalculation_V1),
            new CalculationStepStartJobActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        var continueCalculationMonitor = true;
        var expiryTime = context.CurrentUtcDateTime
            .AddSeconds(executionContext.OrchestrationOptions.CalculationJobStatusExpiryTimeInSeconds);
        while (continueCalculationMonitor && context.CurrentUtcDateTime < expiryTime)
        {
            // Monitor calculation (Databricks)
            var jobRunStatus = await context.CallActivityAsync<JobRunStatus>(
                nameof(CalculationStepGetJobRunStatusActivity_Brs_021_ElectricalHeatingCalculation_V1),
                new CalculationStepGetJobRunStatusActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                    jobRunId),
                _defaultRetryOptions);

            switch (jobRunStatus)
            {
                case JobRunStatus.Pending:
                case JobRunStatus.Queued:
                case JobRunStatus.Running:
                    // Wait for the next checkpoint
                    var nextCheckpoint = context.CurrentUtcDateTime
                        .AddSeconds(executionContext.OrchestrationOptions.CalculationJobStatusPollingIntervalInSeconds);
                    await context.CreateTimer(nextCheckpoint, CancellationToken.None);
                    break;

                case JobRunStatus.Completed:
                    // Suceeded
                    await context.CallActivityAsync(
                        nameof(TransitionStepToTerminatedActivity_Brs_021_ElectricalHeatingCalculation_V1),
                        new TransitionStepToTerminatedActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                            instanceId,
                            CalculationStep.Sequence,
                            OrchestrationStepTerminationStates.Succeeded),
                        _defaultRetryOptions);

                    continueCalculationMonitor = false;
                    break;

                case JobRunStatus.Failed:
                case JobRunStatus.Canceled:
                    // Failed
                    await context.CallActivityAsync(
                        nameof(TransitionStepToTerminatedActivity_Brs_021_ElectricalHeatingCalculation_V1),
                        new TransitionStepToTerminatedActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                            instanceId,
                            CalculationStep.Sequence,
                            OrchestrationStepTerminationStates.Failed),
                        _defaultRetryOptions);
                    await context.CallActivityAsync(
                        nameof(OrchestrationTerminateActivity_Brs_021_ElectricalHeatingCalculation_V1),
                        new OrchestrationTerminateActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                            instanceId,
                            OrchestrationInstanceTerminationStates.Failed),
                        _defaultRetryOptions);

                    // Quit orchestration
                    return $"Error: Job run status '{jobRunStatus}'";
                default:
                    throw new InvalidOperationException("Unknown job run status '{jobRunStatus}'.");
            }
        }

        // Step: Enqueue messages
        if (!executionContext.SkippedStepsBySequence.Contains(EnqueueMessagesStep.Sequence))
        {
            await context.CallActivityAsync(
                nameof(TransitionStepToRunningActivity_Brs_021_ElectricalHeatingCalculation_V1),
                new TransitionStepToRunningActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                    instanceId,
                    EnqueueMessagesStep.Sequence),
                _defaultRetryOptions);
            await context.CallActivityAsync(
                nameof(TransitionStepToTerminatedActivity_Brs_021_ElectricalHeatingCalculation_V1),
                new TransitionStepToTerminatedActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                    instanceId,
                    EnqueueMessagesStep.Sequence,
                    OrchestrationStepTerminationStates.Succeeded),
                _defaultRetryOptions);
        }

        // Terminate
        await context.CallActivityAsync(
            nameof(OrchestrationTerminateActivity_Brs_021_ElectricalHeatingCalculation_V1),
            new OrchestrationTerminateActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                instanceId,
                OrchestrationInstanceTerminationStates.Succeeded),
            _defaultRetryOptions);

        return "Success";
    }

    private static TaskOptions CreateDefaultRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }
}
