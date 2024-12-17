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

using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1;

// TODO: Implement according to guidelines: https://energinet.atlassian.net/wiki/spaces/D3/pages/824803345/Durable+Functions+Development+Guidelines
internal class Orchestration_Brs_021_ElectricalHeatingCalculation_V1
{
    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_021_ElectricalHeatingCalculation_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    internal static StepIdentifierDto[] Steps => [CalculationStep, EnqueueMessagesStep];

    internal static StepIdentifierDto CalculationStep => new(1, "Beregning");

    internal static StepIdentifierDto EnqueueMessagesStep => new(2, "Besked dannelse");

    [Function(nameof(Orchestration_Brs_021_ElectricalHeatingCalculation_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Initialize
        var executionPlan = await context.CallActivityAsync<OrchestrationExecutionPlan>(
            nameof(OrchestrationInitializeActivity_Brs_021_ElectricalHeatingCalculation_V1),
            context.InstanceId,
            _defaultRetryOptions);

        // Step: Calculation
        await context.CallActivityAsync(
            nameof(CalculationStepStartActivity_Brs_021_ElectricalHeatingCalculation_V1),
            context.InstanceId,
            _defaultRetryOptions);
        await context.CallActivityAsync(
            nameof(CalculationStepTerminateActivity_Brs_021_ElectricalHeatingCalculation_V1),
            context.InstanceId,
            _defaultRetryOptions);

        // Step: Enqueue messages
        if (!executionPlan.SkippedStepsBySequence.Contains(EnqueueMessagesStep.Sequence))
        {
            await context.CallActivityAsync(
                nameof(EnqueueMessagesStepStartActivity_Brs_021_ElectricalHeatingCalculation_V1),
                context.InstanceId,
                _defaultRetryOptions);
            await context.CallActivityAsync(
                nameof(EnqueueMessagesStepTerminateActivity_Brs_021_ElectricalHeatingCalculation_V1),
                context.InstanceId,
                _defaultRetryOptions);
        }

        // Terminate
        await context.CallActivityAsync(
            nameof(OrchestrationTerminateActivity_Brs_021_ElectricalHeatingCalculation_V1),
            context.InstanceId,
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
