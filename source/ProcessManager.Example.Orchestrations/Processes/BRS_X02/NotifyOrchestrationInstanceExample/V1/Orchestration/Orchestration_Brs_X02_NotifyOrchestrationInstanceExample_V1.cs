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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Orchestration;

internal class Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1
{
    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_X02_NotifyOrchestrationInstanceExample.V1;

    private readonly TaskRetryOptions _defaultRetryOptions;

    public Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1()
    {
        _defaultRetryOptions = TaskRetryOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }

    [Function(nameof(Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Initialize
        var orchestrationInstanceContext = await InitializeOrchestrationAsync(context);

        // Wait for "ExampleNotifyEvent" notify event
        var hasReceivedExampleNotifyEvent = await new WaitForNotifyEventStep(
                context,
                _defaultRetryOptions,
                orchestrationInstanceContext.OrchestrationInstanceId,
                orchestrationInstanceContext.Options.WaitForExampleNotifyEventTimeout)
            .ExecuteAsync();

        return await TerminateOrchestrationAsync(
            context,
            orchestrationInstanceContext.OrchestrationInstanceId,
            hasReceivedExampleNotifyEvent);
    }

    private async Task<OrchestrationInstanceContext> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(instanceId),
            new TaskOptions(_defaultRetryOptions));

        var orchestrationExecutionPlan = await context.CallActivityAsync<OrchestrationInstanceContext>(
            nameof(GetOrchestrationInstanceContextActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1),
            new GetOrchestrationInstanceContextActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1.ActivityInput(
                instanceId),
            new TaskOptions(_defaultRetryOptions));

        return orchestrationExecutionPlan;
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        bool hasReceivedExampleNotifyEvent)
    {
        var terminationState = hasReceivedExampleNotifyEvent
            ? OrchestrationInstanceTerminationState.Succeeded
            : OrchestrationInstanceTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                instanceId,
                terminationState),
            new TaskOptions(_defaultRetryOptions));

        return hasReceivedExampleNotifyEvent
            ? "Success"
            : "Error: Didn't receive example notify event";
    }
}
