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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X05_FailingOrchestrationInstanceExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1;

internal class Orchestration_Brs_X05_V1
{
    internal const int SuccessStep = 1;
    internal const int FailingStep = 2;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_X05.V1;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_X05_V1()
    {
        _defaultRetryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromMilliseconds(100),
            backoffCoefficient: 1));
    }

    [Function(nameof(Orchestration_Brs_X05_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var instanceId = await InitializeOrchestrationAsync(context);

        await PerformSuccessStepAsync(context, instanceId);

        // This "failing step" fails and throws an exception
        await PerformFailingStepAsync(context, instanceId);

        throw new InvalidOperationException("The orchestration should never reach this point");
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

    private async Task PerformSuccessStepAsync(TaskOrchestrationContext context, OrchestrationInstanceId instanceId)
    {
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_V1),
            new TransitionStepToRunningActivity_V1.ActivityInput(
                instanceId,
                SuccessStep),
            _defaultRetryOptions);

        await context.CallActivityAsync(
            nameof(SuccessActivity_Brs_X05_V1),
            _defaultRetryOptions);

        await context.CallActivityAsync(
            nameof(TransitionStepToTerminatedActivity_V1),
            new TransitionStepToTerminatedActivity_V1.ActivityInput(
                instanceId,
                SuccessStep,
                OrchestrationStepTerminationState.Succeeded),
            _defaultRetryOptions);
    }

    private async Task PerformFailingStepAsync(TaskOrchestrationContext context, OrchestrationInstanceId instanceId)
    {
        await context.CallActivityAsync(
            name: nameof(TransitionStepToRunningActivity_V1),
            input: new TransitionStepToRunningActivity_V1.ActivityInput(
                OrchestrationInstanceId: instanceId,
                StepSequence: FailingStep),
            options: _defaultRetryOptions);

        try
        {
            await context.CallActivityAsync(
                name: nameof(FailingActivity_Brs_X05_V1),
                options: _defaultRetryOptions);
        }
        catch (Exception e)
        {
            await context.CallActivityAsync(
                name: nameof(TransitionStepToTerminatedActivity_V1),
                input: new TransitionStepToTerminatedActivity_V1.ActivityInput(
                    OrchestrationInstanceId: instanceId,
                    StepSequence: FailingStep,
                    TerminationState: OrchestrationStepTerminationState.Failed,
                    TransitionOrchestrationInstanceToFailed: true,
                    CustomState: e.ToString()),
                options: _defaultRetryOptions);

            throw;
        }

        await context.CallActivityAsync(
            name: nameof(TransitionStepToTerminatedActivity_V1),
            input: new TransitionStepToTerminatedActivity_V1.ActivityInput(
                OrchestrationInstanceId: instanceId,
                StepSequence: FailingStep,
                TerminationState: OrchestrationStepTerminationState.Succeeded),
            options: _defaultRetryOptions);
    }
}
