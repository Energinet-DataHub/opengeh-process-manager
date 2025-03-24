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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.ActorRequestProcessExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.ActorRequestProcessExample.V1.Orchestration.Steps;

internal class BusinessValidationStep(
    TaskOrchestrationContext context,
    TaskRetryOptions retryOptions,
    OrchestrationInstanceId instanceId)
        : StepExecutor<PerformBusinessValidationActivity_Brs_X02_ActorRequestProcessExample_V1.ActivityOutput>(context, retryOptions, instanceId)
{
    internal const string StepDescription = "Business validation";
    internal const int StepSequence = 1;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<StepOutput> OnExecuteAsync()
    {
        var businessValidationResult = await Context.CallActivityAsync<PerformBusinessValidationActivity_Brs_X02_ActorRequestProcessExample_V1.ActivityOutput>(
            nameof(PerformBusinessValidationActivity_Brs_X02_ActorRequestProcessExample_V1),
            new PerformBusinessValidationActivity_Brs_X02_ActorRequestProcessExample_V1.ActivityInput(
                InstanceId),
            DefaultRetryOptions);

        var stepTerminationState = businessValidationResult.ValidationErrors.Count == 0
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;

        return new StepOutput(stepTerminationState, businessValidationResult);
    }

    internal record CustomState(
        IReadOnlyCollection<ValidationError> ValidationErrors);
}
