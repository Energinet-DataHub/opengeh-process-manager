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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public static class StepHelper
{
    /// <summary>
    /// Start a step if it is pending and commit the change.
    /// <remarks>Does nothing if the step lifecycle isn't pending.</remarks>
    /// </summary>
    /// <param name="step"></param>
    /// <param name="clock"></param>
    /// <param name="progressRepository"></param>
    public static async Task StartStepAndCommitIfPending(StepInstance step, IClock clock, IOrchestrationInstanceProgressRepository progressRepository)
    {
        if (step.Lifecycle.State == StepInstanceLifecycleState.Pending)
        {
            step.Lifecycle.TransitionToRunning(clock);
            await progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Terminate a step if it is running and commit the change.
    /// </summary>
    /// <param name="step"></param>
    /// <param name="clock"></param>
    /// <param name="progressRepository"></param>
    /// <param name="terminationState">The termination state of the step, defaulting to <see cref="OrchestrationStepTerminationState.Succeeded"/>.</param>
    /// <exception cref="InvalidOperationException">Throws an invalid operation exception if the step isn't running.</exception>
    public static async Task TerminateStepAndCommit(
        StepInstance step,
        IClock clock,
        IOrchestrationInstanceProgressRepository progressRepository,
        OrchestrationStepTerminationState terminationState = OrchestrationStepTerminationState.Succeeded)
    {
        // TODO: Why doesn't this work like the "start step" method? Shouldn't it just do nothing, to guard against idempotency/retries.
        if (step.Lifecycle.State != StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Can only terminate a running step (Step.Id={step.Id}, Step.State={step.Lifecycle.State}).");

        step.Lifecycle.TransitionToTerminated(clock, terminationState);
        await progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }
}
