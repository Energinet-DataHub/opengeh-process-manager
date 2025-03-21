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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Steps;

internal class EnqueueActorMessagesStep(
    TaskOrchestrationContext context,
    TaskRetryOptions retryOptions,
    OrchestrationInstanceId instanceId,
    PerformBusinessValidationActivity_Brs_101_UpdateMeteringPointConnectionState_V1.ActivityOutput validationResult,
    TimeSpan actorMessagesEnqueuedTimeout)
        : StepExecutor(context, retryOptions, instanceId)
{
    internal const string StepDescription = "Udsend beskeder";
    internal const int StepSequence = 2;

    private readonly PerformBusinessValidationActivity_Brs_101_UpdateMeteringPointConnectionState_V1.ActivityOutput _validationResult = validationResult;
    private readonly TimeSpan _actorMessagesEnqueuedTimeout = actorMessagesEnqueuedTimeout;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<OrchestrationStepTerminationState> OnExecuteAsync()
    {
        var enqueueIdempotencyKey = Context.NewGuid();

        if (_validationResult.IsValid)
        {
            await Context.CallActivityAsync(
                nameof(EnqueueActorMessagesActivity_Brs_101_UpdateMeteringPointConnectionState_V1),
                new EnqueueActorMessagesActivity_Brs_101_UpdateMeteringPointConnectionState_V1.ActivityInput(
                    InstanceId,
                    enqueueIdempotencyKey),
                DefaultRetryOptions);
        }
        else
        {
            await Context.CallActivityAsync(
                nameof(EnqueueRejectedActorMessageActivity_Brs_101_UpdateMeteringPointConnectionState_V1),
                new EnqueueRejectedActorMessageActivity_Brs_101_UpdateMeteringPointConnectionState_V1.ActivityInput(
                    InstanceId,
                    enqueueIdempotencyKey,
                    _validationResult.ValidationErrors),
                DefaultRetryOptions);
        }

        // Pattern #5: Human interaction - https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=isolated-process#human
        // If the timeout is reached, an exception will be thrown, and StepExecutor will fail the step and orchestration instance.
        // If we later need a more complex handling of the timeout, we can try/catch the TaskCanceledException here and handle it manually.
        await Context.WaitForExternalEvent<int?>(
            eventName: UpdateMeteringPointConnectionStateNotifyEventV1.OrchestrationInstanceEventName,
            timeout: _actorMessagesEnqueuedTimeout);

        return OrchestrationStepTerminationState.Succeeded;
    }
}
