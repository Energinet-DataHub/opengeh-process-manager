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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DurableTask;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.Shared.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1;

internal class Orchestration_Brs_X03_V1
{
    internal const int EnqueueActorMessagesStep = 1;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_X03.V1;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_X03_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    [Function(nameof(Orchestration_Brs_X03_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetOrchestrationParameterValue<ActorRequestProcessExampleInputV1>();

        // Initialize
        var instanceId = await InitializeOrchestrationAsync(context);

        // Step 1a: Enqueue actor messages
        await EnqueueActorMessages(
            context,
            instanceId,
            input);

        // Step 1b: Wait for actor messages enqueued event
        var hasReceivedActorMessagesEnqueuedEvent = await WaitForActorMessagesEnqueuedEventAsync(
            context,
            instanceId);

        return await TerminateOrchestrationAsync(
            context,
            instanceId,
            hasReceivedActorMessagesEnqueuedEvent,
            input);
    }

    private async Task<OrchestrationInstanceId> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);
        await Task.CompletedTask;

        return instanceId;
    }

    private async Task EnqueueActorMessages(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        ActorRequestProcessExampleInputV1 input)
    {
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_V1),
            new TransitionStepToRunningActivity_V1.ActivityInput(
                instanceId,
                EnqueueActorMessagesStep),
            _defaultRetryOptions);

        var enqueueIdempotencyKey = context.NewGuid();
        await context.CallActivityAsync(
            nameof(EnqueueActorMessagesActivity_Brs_X03_V1),
            new EnqueueActorMessagesActivity_Brs_X03_V1.ActivityInput(
                instanceId,
                enqueueIdempotencyKey,
                input.RequestedByActorNumber,
                input.RequestedByActorRole,
                input.BusinessReason),
            _defaultRetryOptions);

        await Task.CompletedTask;
    }

    private async Task<bool> WaitForActorMessagesEnqueuedEventAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId)
    {
        var waitTimeout = TimeSpan.FromMinutes(1);

        bool hasReceivedActorMessagesEnqueuedEvent;
        try
        {
            await context.WaitForExternalEvent<int?>(
                eventName: ActorRequestProcessExampleNotifyEventsV1.ActorMessagesEnqueued,
                timeout: waitTimeout);
            hasReceivedActorMessagesEnqueuedEvent = true;
        }
        catch (TaskCanceledException)
        {
            var logger = context.CreateReplaySafeLogger<Orchestration_Brs_X03_V1>();
            logger.Log(
                LogLevel.Error,
                "Timeout while waiting for actor messages enqueued event (InstanceId={OrchestrationInstanceId}, Timeout={Timeout}).",
                instanceId.Value,
                waitTimeout.ToString("g"));
            hasReceivedActorMessagesEnqueuedEvent = false;
        }

        var waitForActorMessagesEnqueuedEventTerminationState = hasReceivedActorMessagesEnqueuedEvent
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TransitionStepToTerminatedActivity_V1),
            new TransitionStepToTerminatedActivity_V1.ActivityInput(
                instanceId,
                EnqueueActorMessagesStep,
                waitForActorMessagesEnqueuedEventTerminationState),
            _defaultRetryOptions);

        return hasReceivedActorMessagesEnqueuedEvent;
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        bool hasReceivedExampleNotifyEvent,
        ActorRequestProcessExampleInputV1 input)
    {
        var terminationState = hasReceivedExampleNotifyEvent
            ? OrchestrationInstanceTerminationState.Succeeded
            : OrchestrationInstanceTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                instanceId,
                terminationState),
            _defaultRetryOptions);
        await Task.CompletedTask;

        return hasReceivedExampleNotifyEvent
            ? $"Success (BusinessReason={input.BusinessReason})"
            : "Error: Timeout while waiting for example event";
    }

    private TaskOptions CreateDefaultRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }
}
