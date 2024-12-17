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

using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_X01.Example.V1.Model;
using Energinet.DataHub.Example.Orchestrations.Processes.BRS_X01.Example.V1.Activities;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.Example.Orchestrations.Processes.BRS_X01.Example.V1;

internal class Orchestration_Brs_X01_Example_V1
{
    internal const int StartingStepSequence = 1;
    internal const int SkippableStepSequence = 2;

    [Function(nameof(Orchestration_Brs_X01_Example_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetOrchestrationParameterValue<InputV1>();
        if (input == null)
        {
            return "Error: No input specified.";
        }

        var defaultRetryOptions = CreateDefaultRetryOptions();

        // Initialize
        await context.CallActivityAsync(
            nameof(InitializeOrchestrationActivity_Brs_X01_Example_V1),
            new InitializeOrchestrationActivity_Brs_X01_Example_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.Parse(context.InstanceId))),
            defaultRetryOptions);

        // First Step
        await context.CallActivityAsync(
            nameof(FirstStepStartActivity_Brs_X01_Example_V1),
            new FirstStepStartActivity_Brs_X01_Example_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.Parse(context.InstanceId))),
            defaultRetryOptions);
        await context.CallActivityAsync(
            nameof(FirstStepStopActivity_Brs_X01_Example_V1),
            new FirstStepStopActivity_Brs_X01_Example_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.Parse(context.InstanceId))),
            defaultRetryOptions);

        // Skippable step
        await context.CallActivityAsync(
            nameof(SecondStepStartActivity_Brs_X01_Example_V1),
            new SecondStepStartActivity_Brs_X01_Example_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.Parse(context.InstanceId))),
            defaultRetryOptions);
        await context.CallActivityAsync(
            nameof(SecondStepStopActivity_Brs_X01_Example_V1),
            new SecondStepStopActivity_Brs_X01_Example_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.Parse(context.InstanceId))),
            defaultRetryOptions);

        // Terminate
        await context.CallActivityAsync(
            nameof(TerminateOrchestrationActivity_Brs_X01_Example_V1),
            new TerminateOrchestrationActivity_Brs_X01_Example_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.Parse(context.InstanceId))),
            defaultRetryOptions);

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
