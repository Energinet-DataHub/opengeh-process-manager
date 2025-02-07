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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1.Steps;

#pragma warning disable CA2007
internal class SuccessStep(
    TaskOrchestrationContext context,
    TaskRetryOptions retryOptions,
    OrchestrationInstanceId instanceId)
        : StepExecutor(context, retryOptions, instanceId)
{
    internal const string StepDescription = "Success step";
    internal const int StepSequence = 1;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<OrchestrationStepTerminationState> PerformStepAsync()
    {
        await Context.CallActivityAsync(
            name: nameof(SuccessActivity_Brs_X05_V1),
            options: DefaultRetryOptions);

        return OrchestrationStepTerminationState.Succeeded;
    }
}
#pragma warning restore CA2007
