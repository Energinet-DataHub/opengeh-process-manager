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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;

internal class Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1
{
    internal const int WaitForExampleNotifyEventStepSequence = 1;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_X02_NotifyOrchestrationInstanceExample.V1;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    [Function(nameof(Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetOrchestrationParameterValue<NotifyOrchestrationInstanceExampleInputV1>();

        // Initialize
        var (instanceId, options) = await InitializeOrchestrationAsync(context);

        // Wait for "ExampleNotifyEvent" notify event
        var hasReceivedExampleNotifyEvent = await WaitForExampleNotifyEventAsync(
            context,
            instanceId,
            exampleNotifyEventTimeout: options.WaitForExampleNotifyEventTimeout);

        return await TerminateOrchestrationAsync(
            context,
            instanceId,
            hasReceivedExampleNotifyEvent,
            input);
    }

    private async Task<OrchestrationExecutionPlan> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        var orchestrationExecutionPlan = await context.CallActivityAsync<OrchestrationExecutionPlan>(
            nameof(InitializeOrchestrationActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1),
            new InitializeOrchestrationActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        return orchestrationExecutionPlan;
    }

    private async Task<bool> WaitForExampleNotifyEventAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        TimeSpan exampleNotifyEventTimeout)
    {
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1),
            new TransitionStepToRunningActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1.ActivityInput(
                instanceId,
                WaitForExampleNotifyEventStepSequence),
            _defaultRetryOptions);

        bool hasReceivedExampleNotifyEvent;
        try
        {
            await context.WaitForExternalEvent<int?>(
                eventName: NotifyOrchestrationInstanceExampleNotifyEventsV1.ExampleNotifyEvent,
                timeout: exampleNotifyEventTimeout);
            hasReceivedExampleNotifyEvent = true;
        }
        catch (TaskCanceledException)
        {
            var logger = context.CreateReplaySafeLogger<Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1>();
            logger.Log(
                LogLevel.Error,
                "Timeout while waiting for example notify event (InstanceId={OrchestrationInstanceId}, Timeout={Timeout}).",
                instanceId.Value,
                exampleNotifyEventTimeout.ToString("g"));
            hasReceivedExampleNotifyEvent = false;
        }

        var waitForExampleNotifyEventTerminationState = hasReceivedExampleNotifyEvent
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TransitionStepToTerminatedActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1),
            new TransitionStepToTerminatedActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1.ActivityInput(
                instanceId,
                WaitForExampleNotifyEventStepSequence,
                waitForExampleNotifyEventTerminationState),
            _defaultRetryOptions);

        return hasReceivedExampleNotifyEvent;
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        bool hasReceivedExampleNotifyEvent,
        NotifyOrchestrationInstanceExampleInputV1 input)
    {
        var terminationState = hasReceivedExampleNotifyEvent
            ? OrchestrationInstanceTerminationState.Succeeded
            : OrchestrationInstanceTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TerminateOrchestrationActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1),
            new TerminateOrchestrationActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1.ActivityInput(
                instanceId,
                terminationState),
            _defaultRetryOptions);

        return hasReceivedExampleNotifyEvent
            ? $"Success (BusinessReason={input.InputString})"
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
