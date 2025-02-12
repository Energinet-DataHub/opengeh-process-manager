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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.EnqueActorMessagesStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

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
        // 30 seconds interval, backoff coefficient 2.0, 7 retries (initial attempt is included in the maxNumberOfAttempts)
        // 30 seconds * (2^7-1) = 3810 seconds = 63,5 minutes to use all retries
        _defaultRetryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 8,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }

    [Function(nameof(Orchestration_Brs_023_027_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        // Initialize
        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        var executionContext = await context.CallActivityAsync<OrchestrationExecutionContext>(
            nameof(OrchestrationInitializeActivity_Brs_023_027_V1),
            new OrchestrationInitializeActivity_Brs_023_027_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        // Step: Calculation
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_V1),
            new TransitionStepToRunningActivity_V1.ActivityInput(
                instanceId,
                CalculationStepSequence),
            _defaultRetryOptions);

        // Start calculation (Databricks)
        var jobRunId = await context.CallActivityAsync<JobRunId>(
            nameof(CalculationStepStartJobActivity_Brs_023_027_V1),
            new CalculationStepStartJobActivity_Brs_023_027_V1.ActivityInput(
                instanceId,
                executionContext.CalculationId,
                executionContext.UserId),
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
                        nameof(TransitionStepToTerminatedActivity_V1),
                        new TransitionStepToTerminatedActivity_V1.ActivityInput(
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
                        nameof(TransitionStepToTerminatedActivity_V1),
                        new TransitionStepToTerminatedActivity_V1.ActivityInput(
                            instanceId,
                            CalculationStepSequence,
                            OrchestrationStepTerminationState.Failed),
                        _defaultRetryOptions);
                    await context.CallActivityAsync(
                        nameof(TransitionOrchestrationToTerminatedActivity_V1),
                        new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                            instanceId,
                            OrchestrationInstanceTerminationState.Failed),
                        _defaultRetryOptions);

                    // Quit orchestration
                    return $"Error: Job run status '{jobRunStatus}'";
                default:
                    throw new InvalidOperationException("Unknown job run status '{jobRunStatus}'.");
            }
        }

        var messagesSuccessfullyEnqueued = true;

        // Step: Enqueue messages
        if (!executionContext.SkippedStepsBySequence.Contains(EnqueueActorMessagesStepSequence))
        {
            var calculationData = new CalculationEnqueueActorMessagesV1(
                CalculationId: executionContext.CalculationId);

            messagesSuccessfullyEnqueued = await TryEnqueueMessagesAsync(
                context: context,
                instanceId: instanceId,
                timeout: TimeSpan.FromSeconds(executionContext.OrchestrationOptions.MessagesEnqueuingExpiryTimeInSeconds),
                calculationData: calculationData);

            if (messagesSuccessfullyEnqueued)
            {
                var integrationEventIdempotencyKey = context.NewGuid();

                // Messages have been prepared for the actors, we need to inform the other subsystems
                await context.CallActivityAsync(
                    nameof(PublishCalculationEnqueueCompletedActivity_brs_023_027_V1),
                    new PublishCalculationEnqueueCompletedActivity_brs_023_027_V1.ActivityInput(
                        instanceId,
                        executionContext.CalculationId,
                        integrationEventIdempotencyKey),
                    _defaultRetryOptions);
            }
        }

        var terminationState = messagesSuccessfullyEnqueued
            ? OrchestrationInstanceTerminationState.Succeeded
            : OrchestrationInstanceTerminationState.Failed;

        // Terminate
        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                instanceId,
                terminationState),
            _defaultRetryOptions);

        return "Success";
    }

    private async Task<bool> TryEnqueueMessagesAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        TimeSpan timeout,
        CalculationEnqueueActorMessagesV1 calculationData)
    {
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_V1),
            new TransitionStepToRunningActivity_V1.ActivityInput(
                instanceId,
                EnqueueActorMessagesStepSequence),
            _defaultRetryOptions);

        var idempotencyKey = context.NewGuid();

        await context.CallActivityAsync(
            nameof(EnqueueActorMessagesActivity_Brs_023_027_V1),
            new EnqueueActorMessagesActivity_Brs_023_027_V1.ActivityInput(
                instanceId,
                calculationData,
                idempotencyKey),
            _defaultRetryOptions);

        var success = false;
        try
        {
            var enqueueEvent = await context.WaitForExternalEvent<CalculationEnqueueActorMessagesCompletedNotifyEventV1>(
                eventName: CalculationEnqueueActorMessagesCompletedNotifyEventV1.EventName,
                timeout: timeout);

            success = enqueueEvent.Success;
        }
        catch (TaskCanceledException)
        {
            var logger = context.CreateReplaySafeLogger<Orchestration_Brs_023_027_V1>();
            logger.Log(
                LogLevel.Error,
                "Timeout while waiting for enqueue actor messages to complete (InstanceId={OrchestrationInstanceId}, Timeout={Timeout}).",
                instanceId.Value,
                timeout.ToString("g"));
        }

        var enqueueActorMessagesTerminationState = success
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TransitionStepToTerminatedActivity_V1),
            new TransitionStepToTerminatedActivity_V1.ActivityInput(
                instanceId,
                EnqueueActorMessagesStepSequence,
                enqueueActorMessagesTerminationState),
            _defaultRetryOptions);

        return success;
    }
}
