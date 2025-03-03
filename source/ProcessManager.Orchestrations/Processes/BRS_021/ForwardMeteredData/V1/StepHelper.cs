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
    public static async Task StartStep(StepInstance step, IClock clock, IOrchestrationInstanceProgressRepository progressRepository)
    {
        if (step.Lifecycle.State == StepInstanceLifecycleState.Pending)
            step.Lifecycle.TransitionToRunning(clock);
        await progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    public static async Task TerminateStep(StepInstance step, IClock clock, IOrchestrationInstanceProgressRepository progressRepository)
    {
        if (step.Lifecycle.State != StepInstanceLifecycleState.Running)
            throw new InvalidOperationException("Can only terminate a running step");

        step.Lifecycle.TransitionToTerminated(clock, OrchestrationStepTerminationState.Succeeded);

        await progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }
}
