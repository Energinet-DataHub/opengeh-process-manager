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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1.Steps;

internal class EnqueueActorMessagesStep(
    TaskOrchestrationContext context,
    TaskRetryOptions retryOptions,
    OrchestrationInstanceId instanceId,
    IReadOnlyCollection<ValidationError> validationErrors)
        : StepExecutor(context, retryOptions, instanceId)
{
    internal const string StepDescription = "Enqueue actor messages";
    internal const int StepSequence = 2;

    private readonly IReadOnlyCollection<ValidationError> _validationErrors = validationErrors;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<OrchestrationStepTerminationState> OnExecuteAsync()
    {
        var enqueueIdempotencyKey = Context.NewGuid();

        var succeededBusinessValidation = _validationErrors.Count == 0;
        if (succeededBusinessValidation)
        {
            await Context.CallActivityAsync(
                nameof(EnqueueActorMessagesActivity_Brs_X03_V1),
                new EnqueueActorMessagesActivity_Brs_X03_V1.ActivityInput(
                    InstanceId,
                    enqueueIdempotencyKey),
                DefaultRetryOptions);
        }
        else
        {
            await Context.CallActivityAsync(
                nameof(EnqueueRejectedActorMessageActivity_Brs_X03_V1),
                new EnqueueRejectedActorMessageActivity_Brs_X03_V1.ActivityInput(
                    InstanceId,
                    enqueueIdempotencyKey,
                    _validationErrors),
                DefaultRetryOptions);
        }

        var waitTimeout = TimeSpan.FromMinutes(2);

        // If wait timeout is reached, an exception will be thrown, and the StepExecutorBase will fail the orchestration.
        await Context.WaitForExternalEvent<int?>(
            eventName: ActorRequestProcessExampleNotifyEventV1.OrchestrationInstanceEventName,
            timeout: waitTimeout);

        return OrchestrationStepTerminationState.Succeeded;
    }
}
