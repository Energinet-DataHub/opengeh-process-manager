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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Steps;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "ConfigureAwait must not be used in durable function code")]
internal class BusinessValidationStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId)
        : StepExecutor<PerformBusinessValidationActivity_Brs_028_V1.ActivityOutput>(
            context,
            defaultRetryOptions,
            instanceId)
{
    internal const string StepDescription = "Forretningsvalidering";
    internal const int StepSequence = 1;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<StepOutput> OnExecuteAsync()
    {
        var validationResult = await Context.CallActivityAsync<PerformBusinessValidationActivity_Brs_028_V1.ActivityOutput>(
            nameof(PerformBusinessValidationActivity_Brs_028_V1),
            new PerformBusinessValidationActivity_Brs_028_V1.ActivityInput(
                InstanceId,
                StepSequence),
            DefaultRetryOptions);

        var asyncValidationTerminationState = validationResult.IsValid
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;

        return new StepOutput(asyncValidationTerminationState, validationResult);
    }
}
