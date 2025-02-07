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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Steps;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1;

internal class Orchestration_Brs_X01_InputExample_V1
{
    private readonly TaskRetryOptions _defaultRetryOptions;

    public Orchestration_Brs_X01_InputExample_V1()
    {
        _defaultRetryOptions = TaskRetryOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }

    [Function(nameof(Orchestration_Brs_X01_InputExample_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var orchestrationInstanceContext = await InitializeOrchestrationAsync(context);

        // First Step
        await new FirstStep(context, _defaultRetryOptions, orchestrationInstanceContext.OrchestrationInstanceId)
            .ExecuteStepAsync();

        // Skippable step
        if (!orchestrationInstanceContext.SkippedStepsBySequence.Contains(SkippableStep.StepSequence))
        {
            await new SkippableStep(context, _defaultRetryOptions, orchestrationInstanceContext.OrchestrationInstanceId)
                .ExecuteStepAsync();
        }

        // Terminate
        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                orchestrationInstanceContext.OrchestrationInstanceId,
                OrchestrationInstanceTerminationState.Succeeded),
            new TaskOptions(_defaultRetryOptions));

        return "Success";
    }

    private async Task<OrchestrationInstanceContext> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(
                instanceId),
            new TaskOptions(_defaultRetryOptions));

        var orchestrationInstanceContext = await context.CallActivityAsync<OrchestrationInstanceContext>(
            nameof(GetOrchestrationInstanceContextActivity_Brs_X01_InputExample_V1),
            new GetOrchestrationInstanceContextActivity_Brs_X01_InputExample_V1.ActivityInput(
                instanceId),
            new TaskOptions(_defaultRetryOptions));

        return orchestrationInstanceContext;
    }
}
