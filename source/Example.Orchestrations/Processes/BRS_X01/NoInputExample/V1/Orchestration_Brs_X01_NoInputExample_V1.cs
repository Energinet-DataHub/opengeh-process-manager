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

using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1;

// TODO: Implement according to guidelines: https://energinet.atlassian.net/wiki/spaces/D3/pages/824803345/Durable+Functions+Development+Guidelines
internal class Orchestration_Brs_X01_NoInputExample_V1
{
    internal const int FirstStepSequence = 1;
    internal const int SkippableStepSequence = 2;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_X01_NoInputExample_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    [Function(nameof(Orchestration_Brs_X01_NoInputExample_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        // Initialize
        var executionPlan = await context.CallActivityAsync<OrchestrationExecutionPlan>(
            nameof(OrchestrationInitializeActivity_Brs_X01_NoInputExample_V1),
            new OrchestrationInitializeActivity_Brs_X01_NoInputExample_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        // First Step
        await context.CallActivityAsync(
            nameof(FirstStepStartActivity_Brs_X01_NoInputExample_V1),
            new FirstStepStartActivity_Brs_X01_NoInputExample_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);
        await context.CallActivityAsync(
            nameof(FirstStepStopActivity_Brs_X01_NoInputExample_V1),
            new FirstStepStopActivity_Brs_X01_NoInputExample_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        // Skippable step
        if (!executionPlan.SkippedStepsBySequence.Contains(SkippableStepSequence))
        {
            await context.CallActivityAsync(
                nameof(SecondStepStartActivity_Brs_X01_NoInputExample_V1),
                new SecondStepStartActivity_Brs_X01_NoInputExample_V1.ActivityInput(
                    instanceId),
                _defaultRetryOptions);
            await context.CallActivityAsync(
                nameof(SecondStepStopActivity_Brs_X01_NoInputExample_V1),
                new SecondStepStopActivity_Brs_X01_NoInputExample_V1.ActivityInput(
                    instanceId),
                _defaultRetryOptions);
        }

        // Terminate
        await context.CallActivityAsync(
            nameof(OrchestrationTerminateActivity_Brs_X01_NoInputExample_V1),
            new OrchestrationTerminateActivity_Brs_X01_NoInputExample_V1.ActivityInput(
                instanceId),
            _defaultRetryOptions);

        return "Success";
    }

    private static TaskOptions CreateDefaultRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }
}
