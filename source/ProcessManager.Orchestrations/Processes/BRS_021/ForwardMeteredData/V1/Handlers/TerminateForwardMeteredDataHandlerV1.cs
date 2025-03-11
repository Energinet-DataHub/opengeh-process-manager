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

        if (orchestrationInstance.Lifecycle.State != OrchestrationInstanceLifecycleState.Running)
            return;

        // Terminate Step: Enqueue actor messages step
        var enqueueActorMessagesStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep);
        await StepHelper.TerminateStepAndCommit(enqueueActorMessagesStep, _clock, _progressRepository).ConfigureAwait(false);

        orchestrationInstance.Lifecycle.TransitionToSucceeded(_clock);
        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }
}
