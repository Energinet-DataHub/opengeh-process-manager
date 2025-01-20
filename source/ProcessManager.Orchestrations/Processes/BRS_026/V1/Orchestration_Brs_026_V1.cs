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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1;

internal class Orchestration_Brs_026_V1
{
    public const int AsyncValidationStepSequence = 1;
    public const int EnqueueActorMessagesStepSequence = 2;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_026.V1;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_026_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    [Function(nameof(Orchestration_Brs_026_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        context.SetCustomStatus(CustomStatus.OrchestrationInstanceStarted);

        var input = context.GetOrchestrationParameterValue<RequestCalculatedEnergyTimeSeriesInputV1>();

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

    private Task<OrchestrationExecutionContext> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        return context.CallActivityAsync<OrchestrationExecutionContext>(
            nameof(StartOrchestrationActivity_Brs_026_V1),
            new StartOrchestrationActivity_Brs_026_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);
    }

    private async Task<PerformAsyncValidationActivity_Brs_026_V1.ActivityOutput> PerformAsynchronousValidationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedEnergyTimeSeriesInputV1 input)
    {
        context.SetCustomStatus(CustomStatus.PerformingAsyncValidation);
        var validationResult = await context.CallActivityAsync<PerformAsyncValidationActivity_Brs_026_V1.ActivityOutput>(
            nameof(PerformAsyncValidationActivity_Brs_026_V1),
            new PerformAsyncValidationActivity_Brs_026_V1.ActivityInput(
                instanceId,
                input),
            _defaultRetryOptions);

        var asyncValidationTerminationState = validationResult.IsValid
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;
        await context.CallActivityAsync(
            nameof(TerminateStepActivity_Brs_026_V1),
            new TerminateStepActivity_Brs_026_V1.ActivityInput(
                instanceId,
                AsyncValidationStepSequence,
                asyncValidationTerminationState),
            _defaultRetryOptions);

        context.SetCustomStatus(validationResult.IsValid
            ? CustomStatus.AsyncValidationSuccess
            : CustomStatus.AsyncValidationFailed);

        return validationResult;
    }

    private async Task EnqueueActorMessagesInEdiAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedEnergyTimeSeriesInputV1 input,
        PerformAsyncValidationActivity_Brs_026_V1.ActivityOutput validationResult)
    {
        if (validationResult.IsValid)
        {
            await context.CallActivityAsync(
                nameof(EnqueueActorMessagesActivity_Brs_026_V1),
                new EnqueueActorMessagesActivity_Brs_026_V1.ActivityInput(
                    instanceId,
                    input),
                _defaultRetryOptions);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(validationResult.ValidationError);

            await context.CallActivityAsync(
                nameof(EnqueueRejectMessageActivity_Brs_026_V1),
                new EnqueueRejectMessageActivity_Brs_026_V1.ActivityInput(
                    instanceId,
                    validationResult.ValidationError),
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
            context.SetCustomStatus(CustomStatus.WaitingForEnqueueActorMessages);
            await context.WaitForExternalEvent<int?>(
                eventName: RequestCalculatedEnergyTimeSeriesNotifyEventsV1.EnqueueActorMessagesCompleted,
                timeout: actorMessagesEnqueuedTimeout);
            wasMessagesEnqueued = true;
        }
        catch (TaskCanceledException)
        {
            var logger = context.CreateReplaySafeLogger<Orchestration_Brs_026_V1>();
            logger.Log(
                LogLevel.Error,
                "Timeout while waiting for enqueue actor messages to complete (InstanceId={OrchestrationInstanceId}, Timeout={Timeout}).",
                instanceId.Value,
                actorMessagesEnqueuedTimeout.ToString("g"));
            wasMessagesEnqueued = false;
        }

        context.SetCustomStatus(wasMessagesEnqueued
            ? CustomStatus.ActorMessagesEnqueued
            : CustomStatus.TimeoutWaitingForEnqueueActorMessages);

        var enqueueActorMessagesTerminationState = wasMessagesEnqueued
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;
        await context.CallActivityAsync(
            nameof(TerminateStepActivity_Brs_026_V1),
            new TerminateStepActivity_Brs_026_V1.ActivityInput(
                instanceId,
                EnqueueActorMessagesStepSequence,
                enqueueActorMessagesTerminationState),
            _defaultRetryOptions);

        return wasMessagesEnqueued;
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedEnergyTimeSeriesInputV1 input,
        bool wasMessagesEnqueued)
    {
        if (!wasMessagesEnqueued)
        {
            await context.CallActivityAsync(
                nameof(TerminateOrchestrationActivity_Brs_026_V1),
                new TerminateOrchestrationActivity_Brs_026_V1.ActivityInput(
                    instanceId,
                    OrchestrationInstanceTerminationState.Failed),
                _defaultRetryOptions);

            return "Error: Timeout while waiting for enqueue actor messages";
        }

        await context.CallActivityAsync(
            nameof(TerminateOrchestrationActivity_Brs_026_V1),
            new TerminateOrchestrationActivity_Brs_026_V1.ActivityInput(
                instanceId,
                OrchestrationInstanceTerminationState.Succeeded),
            _defaultRetryOptions);

        return $"Success (BusinessReason={input.BusinessReason})";
    }

    public static class CustomStatus
    {
        public const string OrchestrationInstanceStarted = "OrchestrationInstanceStarted";
        public const string PerformingAsyncValidation = "PerformingAsyncValidation";
        public const string AsyncValidationSuccess = "AsyncValidationSuccess";
        public const string AsyncValidationFailed = "AsyncValidationFailed";
        public const string WaitingForEnqueueActorMessages = "WaitingForEnqueueActorMessages";
        public const string ActorMessagesEnqueued = "ActorMessagesEnqueued";
        public const string TimeoutWaitingForEnqueueActorMessages = "TimeoutWaitingForEnqueueActorMessages";
    }
}
