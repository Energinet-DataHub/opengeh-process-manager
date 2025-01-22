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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DurableTask;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.EnqueActorMessagesStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1;

// TODO: Implement according to guidelines: https://energinet.atlassian.net/wiki/spaces/D3/pages/824803345/Durable+Functions+Development+Guidelines
internal class Orchestration_Brs_023_027_V1
{
    internal const int CalculationStepSequence = 1;
    internal const int EnqueueActorMessagesStepSequence = 2;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_023_027.V1;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_023_027_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    [Function(nameof(Orchestration_Brs_023_027_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // TODO: For demo purposes; decide if we want to continue injecting parameters
        // OR we want to have a pattern where developers load any info they need from the databae, using the first activity.
        // Currently we inject parameters when an orchestration is started.
        // But 'context.InstanceId' contains the 'OrchestrationInstance.Id' so it is possible to load all
        // information about an 'OrchestrationInstance' in activities and use any information (e.g. UserIdentity).
        var orchestrationInput = context.GetOrchestrationParameterValue<CalculationInputV1>();
        if (orchestrationInput == null)
            return "Error: No input specified.";

        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        // Initialize
        var executionContext = await context.CallActivityAsync<OrchestrationExecutionContext>(
            nameof(OrchestrationInitializeActivity_Brs_023_027_V1),
            new OrchestrationInitializeActivity_Brs_023_027_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        // Step: Calculation
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_Brs_023_027_V1),
            new TransitionStepToRunningActivity_Brs_023_027_V1.ActivityInput(
                instanceId,
                CalculationStepSequence),
            _defaultRetryOptions);

        // Start calculation (Databricks)
        var jobRunId = await context.CallActivityAsync<JobRunId>(
            nameof(CalculationStepStartJobActivity_Brs_023_027_V1),
            new CalculationStepStartJobActivity_Brs_023_027_V1.ActivityInput(
                executionContext.CalculationId,
                executionContext.UserId,
                orchestrationInput),
            _defaultRetryOptions);

        // TODO: We currently have removed the following functionality compared to the orchestration in Wholesale:
        //  - Updating job status in SQL database; it can be found in durable function monitor if we need to
        //  - "Restart" the calculation job if it was canceled; not sure this is a valid feature anymore
        var continueCalculationMonitor = true;
        var expiryTime = context.CurrentUtcDateTime
            .AddSeconds(executionContext.OrchestrationOptions.CalculationJobStatusExpiryTimeInSeconds);
        while (continueCalculationMonitor && context.CurrentUtcDateTime < expiryTime)
        {
            // Monitor calculation (Databricks)
            var jobRunStatus = await context.CallActivityAsync<JobRunStatus>(
                nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1),
                new CalculationStepGetJobRunStatusActivity_Brs_023_027_V1.ActivityInput(
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
                    // Succeeded
                    await context.CallActivityAsync(
                        nameof(TransitionStepToTerminatedActivity_Brs_023_027_V1),
                        new TransitionStepToTerminatedActivity_Brs_023_027_V1.ActivityInput(
                            instanceId,
                            CalculationStepSequence,
                            OrchestrationStepTerminationState.Succeeded),
                        _defaultRetryOptions);

                    continueCalculationMonitor = false;
                    break;

                case JobRunStatus.Failed:
                case JobRunStatus.Canceled:
                    // Failed
                    await context.CallActivityAsync(
                        nameof(TransitionStepToTerminatedActivity_Brs_023_027_V1),
                        new TransitionStepToTerminatedActivity_Brs_023_027_V1.ActivityInput(
                            instanceId,
                            CalculationStepSequence,
                            OrchestrationStepTerminationState.Failed),
                        _defaultRetryOptions);
                    await context.CallActivityAsync(
                        nameof(OrchestrationTerminateActivity_Brs_023_027_V1),
                        new OrchestrationTerminateActivity_Brs_023_027_V1.ActivityInput(
                            instanceId,
                            OrchestrationInstanceTerminationState.Failed),
                        _defaultRetryOptions);

                    // Quit orchestration
                    return $"Error: Job run status '{jobRunStatus}'";
                default:
                    throw new InvalidOperationException("Unknown job run status '{jobRunStatus}'.");
            }
        }

        // Step: Enqueue messages
        if (!executionContext.SkippedStepsBySequence.Contains(EnqueueActorMessagesStepSequence))
        {
            await context.CallActivityAsync(
                nameof(TransitionStepToRunningActivity_Brs_023_027_V1),
                new TransitionStepToRunningActivity_Brs_023_027_V1.ActivityInput(
                    instanceId,
                    EnqueueActorMessagesStepSequence),
                _defaultRetryOptions);

            var calculationData = new CalculatedDataForCalculationTypeV1(
                CalculationId: executionContext.CalculationId,
                CalculationType: orchestrationInput.CalculationType);

            var idempotencyKey = context.NewGuid();

            await context.CallActivityAsync(
                nameof(EnqueueActorMessagesActivity_Brs_023_027_V1),
                new EnqueueActorMessagesActivity_Brs_023_027_V1.ActivityInput(
                    instanceId,
                    calculationData,
                    idempotencyKey),
                _defaultRetryOptions);

            // TODO: Wait for actor messages enqueued notify event

            await context.CallActivityAsync(
                nameof(TransitionStepToTerminatedActivity_Brs_023_027_V1),
                new TransitionStepToTerminatedActivity_Brs_023_027_V1.ActivityInput(
                    instanceId,
                    EnqueueActorMessagesStepSequence,
                    OrchestrationStepTerminationState.Succeeded),
                _defaultRetryOptions);
        }

        // TODO: Publish CalculationCompleted integration event (should this also be published if enqueue messages is skipped?)

        // Terminate
        await context.CallActivityAsync(
            nameof(OrchestrationTerminateActivity_Brs_023_027_V1),
            new OrchestrationTerminateActivity_Brs_023_027_V1.ActivityInput(
                instanceId,
                OrchestrationInstanceTerminationState.Succeeded),
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
