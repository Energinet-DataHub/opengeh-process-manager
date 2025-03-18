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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03.FailingOrchestrationInstanceExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03.FailingOrchestrationInstanceExample.V1.Steps;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03.FailingOrchestrationInstanceExample.V1;

internal class Orchestration_Brs_X03_FailingOrchestrationInstanceExample_V1
{
    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_X03_FailingOrchestrationInstanceExample.V1;

    private readonly TaskRetryOptions _defaultRetryOptions;

    public Orchestration_Brs_X03_FailingOrchestrationInstanceExample_V1()
    {
        _defaultRetryOptions = TaskRetryOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromMilliseconds(100),
            backoffCoefficient: 1));
    }

    [Function(nameof(Orchestration_Brs_X03_FailingOrchestrationInstanceExample_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var instanceId = await InitializeOrchestrationAsync(context);

        await new SuccessStep(context, _defaultRetryOptions, instanceId).ExecuteAsync();

        // This "failing step" fails and throws an exception
        await new FailingStep(context, _defaultRetryOptions, instanceId).ExecuteAsync();

        throw new InvalidOperationException("The orchestration should never reach this point");
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
