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

using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class TerminateForwardMeteredDataHandlerV1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;

    public async Task HandleAsync(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // Do nothing if the orchestration instance is already terminated
        if (orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated)
            return;

        // Throw if the orchestration instance is not running (it should not be possible to be in pending/queued state at this point).
        if (orchestrationInstance.Lifecycle.State != OrchestrationInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Received notify but the orchestration instance {orchestrationInstanceId} is not running.");

        await TerminateEnqueueActorMessagesStep(orchestrationInstance).ConfigureAwait(false);

        await TerminateOrchestrationInstance(orchestrationInstance).ConfigureAwait(false);
    }

    private async Task TerminateOrchestrationInstance(OrchestrationInstance orchestrationInstance)
    {
        var businessValidationStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.BusinessValidationStep);

        var succeededBusinessValidation = businessValidationStep.Lifecycle is { TerminationState: OrchestrationStepTerminationState.Succeeded };
        if (succeededBusinessValidation)
            orchestrationInstance.Lifecycle.TransitionToSucceeded(_clock);
        else
            orchestrationInstance.Lifecycle.TransitionToFailed(_clock);

        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    private async Task TerminateEnqueueActorMessagesStep(OrchestrationInstance orchestrationInstance)
    {
        var enqueueActorMessagesStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep);

        if (enqueueActorMessagesStep.Lifecycle.State != StepInstanceLifecycleState.Terminated)
            await StepHelper.TerminateStepAndCommit(enqueueActorMessagesStep, _clock, _progressRepository).ConfigureAwait(false);
    }
}
