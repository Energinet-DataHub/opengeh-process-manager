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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Models;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1;

// TODO: Implement according to guidelines: https://energinet.atlassian.net/wiki/spaces/D3/pages/824803345/Durable+Functions+Development+Guidelines
internal class Orchestration_Brs_028_V1
{
    public const int BusinessValidationStepSequence = 1;
    public const int EnqueueActorMessagesStepSequence = 2;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_028.V1;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_028_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    [Function(nameof(Orchestration_Brs_028_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetOrchestrationParameterValue<RequestCalculatedWholesaleServicesInputV1>();

        var (instanceId, options) = await InitializeOrchestrationAsync(context);

        var validationResult = await PerformAsynchronousValidationAsync(context, instanceId, input);
        await EnqueueActorMessagesInEdiAsync(context, instanceId, input, validationResult);

        var wasMessagesEnqueued = await WaitForEnqueueActorMessagesResponseFromEdiAsync(
            context,
            options.EnqueueActorMessagesTimeout,
            instanceId);

        return await TerminateOrchestrationAsync(context, instanceId, input, wasMessagesEnqueued);
    }

    private static TaskOptions CreateDefaultRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }

    private async Task<OrchestrationInstanceContext> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        var orchestrationInstanceContext = await context.CallActivityAsync<OrchestrationInstanceContext>(
            nameof(GetOrchestrationInstanceContextActivity_Brs_028_V1),
            new GetOrchestrationInstanceContextActivity_Brs_028_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        return orchestrationInstanceContext;
    }

    private async Task<PerformBusinessValidationActivity_Brs_028_V1.ActivityOutput> PerformAsynchronousValidationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedWholesaleServicesInputV1 input)
    {
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_V1),
            new TransitionStepToRunningActivity_V1.ActivityInput(
                instanceId,
                BusinessValidationStepSequence),
            _defaultRetryOptions);

        var validationResult = await context.CallActivityAsync<PerformBusinessValidationActivity_Brs_028_V1.ActivityOutput>(
            nameof(PerformBusinessValidationActivity_Brs_028_V1),
            new PerformBusinessValidationActivity_Brs_028_V1.ActivityInput(
                instanceId,
                BusinessValidationStepSequence,
                input),
            _defaultRetryOptions);

        var asyncValidationTerminationState = validationResult.IsValid
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;
        await context.CallActivityAsync(
            nameof(TransitionStepToTerminatedActivity_V1),
            new TransitionStepToTerminatedActivity_V1.ActivityInput(
                instanceId,
                BusinessValidationStepSequence,
                asyncValidationTerminationState),
            _defaultRetryOptions);

        return validationResult;
    }

    private async Task EnqueueActorMessagesInEdiAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedWholesaleServicesInputV1 input,
        PerformBusinessValidationActivity_Brs_028_V1.ActivityOutput validationResult)
    {
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_V1),
            new TransitionStepToRunningActivity_V1.ActivityInput(
                instanceId,
                EnqueueActorMessagesStepSequence),
            _defaultRetryOptions);

        var idempotencyKey = context.NewGuid();
        if (validationResult.IsValid)
        {
            await context.CallActivityAsync(
                nameof(EnqueueActorMessagesActivity_Brs_028_V1),
                new EnqueueActorMessagesActivity_Brs_028_V1.ActivityInput(
                    instanceId,
                    input,
                    idempotencyKey),
                _defaultRetryOptions);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(validationResult.ValidationErrors);

            await context.CallActivityAsync(
                nameof(EnqueueRejectMessageActivity_Brs_028_V1),
                new EnqueueRejectMessageActivity_Brs_028_V1.ActivityInput(
                    instanceId,
                    validationResult.ValidationErrors,
                    idempotencyKey),
                _defaultRetryOptions);
        }
    }

    /// <summary>
    /// Pattern #5: Human interaction - https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=isolated-process#human
    /// </summary>
    private async Task<bool> WaitForEnqueueActorMessagesResponseFromEdiAsync(
        TaskOrchestrationContext context,
        TimeSpan actorMessagesEnqueuedTimeout,
        OrchestrationInstanceId instanceId)
    {
        bool wasMessagesEnqueued;
        try
        {
            await context.WaitForExternalEvent<int?>(
                eventName: RequestCalculatedWholesaleServicesNotifyEventsV1.EnqueueActorMessagesCompleted,
                timeout: actorMessagesEnqueuedTimeout);
            wasMessagesEnqueued = true;
        }
        catch (TaskCanceledException)
        {
            var logger = context.CreateReplaySafeLogger<Orchestration_Brs_028_V1>();
            logger.Log(
                LogLevel.Error,
                "Timeout while waiting for enqueue actor messages to complete (InstanceId={OrchestrationInstanceId}, Timeout={Timeout}).",
                instanceId.Value,
                actorMessagesEnqueuedTimeout.ToString("g"));
            wasMessagesEnqueued = false;
        }

        var enqueueActorMessagesTerminationState = wasMessagesEnqueued
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;
        await context.CallActivityAsync(
            nameof(TransitionStepToTerminatedActivity_V1),
            new TransitionStepToTerminatedActivity_V1.ActivityInput(
                instanceId,
                EnqueueActorMessagesStepSequence,
                enqueueActorMessagesTerminationState),
            _defaultRetryOptions);

        return wasMessagesEnqueued;
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedWholesaleServicesInputV1 input,
        bool wasMessagesEnqueued)
    {
        var orchestrationTerminationState = wasMessagesEnqueued
            ? OrchestrationInstanceTerminationState.Succeeded
            : OrchestrationInstanceTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                instanceId,
                orchestrationTerminationState),
            _defaultRetryOptions);

        return wasMessagesEnqueued
            ? $"Success (BusinessReason={input.BusinessReason})"
            : "Error: Timeout while waiting for enqueue actor messages";
    }
}
