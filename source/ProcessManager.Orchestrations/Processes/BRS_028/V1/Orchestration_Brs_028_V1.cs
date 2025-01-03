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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DurableTask;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_028.V1.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_028.V1;

// TODO: Implement according to guidelines: https://energinet.atlassian.net/wiki/spaces/D3/pages/824803345/Durable+Functions+Development+Guidelines
internal class Orchestration_Brs_028_V1
{
    public const int AsyncValidationStepSequence = 1;
    public const int EnqueueMessagesStepSequence = 2;

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

        var instanceId = await InitializeOrchestrationAsync(context);

        var isValidAsynchronousValidation = await PerformAsynchronousValidationAsync(context, instanceId, input);
        await EnqueueMessagesInEdiAsync(context, instanceId, input, isValidAsynchronousValidation);

        var wasMessagesEnqueued = await WaitForEnqueueMessagesResponseFromEdiAsync(context, instanceId);
        return await TerminateOrchestrationAsync(context, instanceId, input, wasMessagesEnqueued);
    }

    private static TaskOptions CreateDefaultRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }

    private async Task<OrchestrationInstanceId> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(StartOrchestrationActivity_Brs_028_V1),
            new StartOrchestrationActivity_Brs_028_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        return instanceId;
    }

    private async Task<bool> PerformAsynchronousValidationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedWholesaleServicesInputV1 input)
    {
        var isValid = await context.CallActivityAsync<bool>(
            nameof(PerformAsyncValidationActivity_Brs_028_V1),
            new PerformAsyncValidationActivity_Brs_028_V1.ActivityInput(
                instanceId,
                input),
            _defaultRetryOptions);

        var asyncValidationTerminationState = isValid
            ? OrchestrationStepTerminationStates.Succeeded
            : OrchestrationStepTerminationStates.Failed;
        await context.CallActivityAsync(
            nameof(TerminateStepActivity_Brs_028_V1),
            new TerminateStepActivity_Brs_028_V1.ActivityInput(
                instanceId,
                AsyncValidationStepSequence,
                asyncValidationTerminationState),
            _defaultRetryOptions);

        return isValid;
    }

    private async Task EnqueueMessagesInEdiAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedWholesaleServicesInputV1 input,
        bool isValidAsyncValidation)
    {
        if (isValidAsyncValidation)
        {
            await context.CallActivityAsync(
                nameof(EnqueueMessagesActivity_Brs_028_V1),
                new EnqueueMessagesActivity_Brs_028_V1.ActivityInput(
                    instanceId,
                    input),
                _defaultRetryOptions);
        }
        else
        {
            await context.CallActivityAsync(
                nameof(EnqueueRejectMessageActivity_Brs_028_V1),
                new EnqueueRejectMessageActivity_Brs_028_V1.ActivityInput(
                    instanceId,
                    "Validation error"),
                _defaultRetryOptions);
        }
    }

    private async Task<bool> WaitForEnqueueMessagesResponseFromEdiAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId)
    {
        // TODO: Use monitor pattern to wait for "notify" from EDI
        var waitForMessagesEnqueued = context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
        await waitForMessagesEnqueued;

        return true;
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        RequestCalculatedWholesaleServicesInputV1 input,
        bool wasMessagesEnqueued)
    {
        var enqueueMessagesTerminationState = wasMessagesEnqueued
            ? OrchestrationStepTerminationStates.Succeeded
            : OrchestrationStepTerminationStates.Failed;
        await context.CallActivityAsync(
            nameof(TerminateStepActivity_Brs_028_V1),
            new TerminateStepActivity_Brs_028_V1.ActivityInput(
                instanceId,
                EnqueueMessagesStepSequence,
                enqueueMessagesTerminationState),
            _defaultRetryOptions);

        if (!wasMessagesEnqueued)
        {
            var logger = context.CreateReplaySafeLogger<Orchestration_Brs_028_V1>();
            logger.Log(
                LogLevel.Warning,
                "Timeout while waiting for enqueue messages to complete (InstanceId={OrchestrationInstanceId}).",
                instanceId.Value);

            await context.CallActivityAsync(
                nameof(TerminateOrchestrationActivity_Brs_028_V1),
                new TerminateOrchestrationActivity_Brs_028_V1.ActivityInput(
                    instanceId,
                    OrchestrationInstanceTerminationStates.Failed),
                _defaultRetryOptions);

            return "Error: Timeout while waiting for enqueue messages";
        }
        else
        {
            await context.CallActivityAsync(
                nameof(TerminateOrchestrationActivity_Brs_028_V1),
                new TerminateOrchestrationActivity_Brs_028_V1.ActivityInput(
                    instanceId,
                    OrchestrationInstanceTerminationStates.Succeeded),
                _defaultRetryOptions);

            return $"Success (BusinessReason={input.BusinessReason})";
        }
    }
}
