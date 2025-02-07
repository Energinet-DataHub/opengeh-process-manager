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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Steps;

#pragma warning disable CA2007
public class ExampleStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId)
        : StepExecutor<int>(context, defaultRetryOptions, instanceId)
{
    internal const string StepDescription = "Example step";
    internal const int StepSequence = 1;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<(OrchestrationStepTerminationState StepTerminationState, int StepOutput)> PerformStepWithOutputAsync()
    {
        var workResult = await Context.CallActivityAsync<PerformCalculationActivity_Brs_X01_NoInputExample_V1.ActivityOutput>(
                nameof(PerformCalculationActivity_Brs_X01_NoInputExample_V1),
                new PerformCalculationActivity_Brs_X01_NoInputExample_V1.ActivityInput(
                    InstanceId),
                DefaultRetryOptions);

        return (OrchestrationStepTerminationState.Succeeded, workResult.CalculationResult);
    }
}
#pragma warning restore CA2007
