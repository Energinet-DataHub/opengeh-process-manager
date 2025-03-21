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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.Orchestration.Steps;

internal class EnqueueActorMessagesStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId,
    PerformBusinessValidationActivity_Brs_026_V1.ActivityOutput validationResult,
    TimeSpan actorMessagesEnqueuedTimeout)
        : StepExecutor(context, defaultRetryOptions, instanceId)
{
    internal const string StepDescription = "Udsend beskeder";
    internal const int StepSequence = 2;

    private readonly TimeSpan _actorMessagesEnqueuedTimeout = actorMessagesEnqueuedTimeout;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<OrchestrationStepTerminationState> OnExecuteAsync()
    {
        var idempotencyKey = Context.NewGuid();
        if (validationResult.IsValid)
        {
            await Context.CallActivityAsync(
                nameof(EnqueueActorMessagesActivity_Brs_026_V1),
                new EnqueueActorMessagesActivity_Brs_026_V1.ActivityInput(
                    InstanceId,
                    idempotencyKey),
                DefaultRetryOptions);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(validationResult.ValidationErrors);

            await Context.CallActivityAsync(
                nameof(EnqueueRejectMessageActivity_Brs_026_V1),
                new EnqueueRejectMessageActivity_Brs_026_V1.ActivityInput(
                    InstanceId,
                    validationResult.ValidationErrors,
                    idempotencyKey),
                DefaultRetryOptions);
        }

        // Pattern #5: Human interaction - https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=isolated-process#human
        // If the timeout is reached, an exception will be thrown, and StepExecutor will fail the step and orchestration instance.
        // If we later need a more complex handling of the timeout, we can try/catch the TaskCanceledException here and handle it manually.
        await Context.WaitForExternalEvent<int?>(
            eventName: RequestCalculatedEnergyTimeSeriesNotifyEventV1.OrchestrationInstanceEventName,
            timeout: _actorMessagesEnqueuedTimeout);

        return OrchestrationStepTerminationState.Succeeded;
    }
}
