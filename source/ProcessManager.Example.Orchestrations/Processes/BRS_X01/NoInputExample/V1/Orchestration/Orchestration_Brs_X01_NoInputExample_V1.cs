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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Orchestration;

// Implement according to guidelines: https://energinet.atlassian.net/wiki/spaces/D3/pages/824803345/Durable+Functions+Development+Guidelines
internal class Orchestration_Brs_X01_NoInputExample_V1
{
    private readonly TaskRetryOptions _defaultRetryOptions;

    public Orchestration_Brs_X01_NoInputExample_V1()
    {
        _defaultRetryOptions = TaskRetryOptions.FromRetryPolicy(new RetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(30),
                backoffCoefficient: 2.0));
    }

    [Function(nameof(Orchestration_Brs_X01_NoInputExample_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        // Initialize
        var orchestrationInstanceId = await InitializeOrchestrationAsync(context);

        var exampleStepResult = await new ExampleStep(context, _defaultRetryOptions, orchestrationInstanceId)
            .ExecuteAsync();

        // Terminate
        return await TerminateOrchestrationAsync(context, instanceId, exampleStepResult);
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        int exampleStepResult)
    {
        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                instanceId,
                OrchestrationInstanceTerminationState.Succeeded),
            new TaskOptions(_defaultRetryOptions));

        return $"Success (example step result: {exampleStepResult})";
    }

    private async Task<OrchestrationInstanceId> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(
                instanceId),
            new TaskOptions(_defaultRetryOptions));
        await Task.CompletedTask;

        return instanceId;
    }
}
