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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Orchestration.Steps;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities;

/// <summary>
/// Get the <see cref="OrchestrationInstanceContext"/> for the orchestration instance.
/// </summary>
internal class SkipEnqueueActorMessagesStepActivity_Brs_021_ElectricalHeatingCalculation_V1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;

    [Function(nameof(SkipEnqueueActorMessagesStepActivity_Brs_021_ElectricalHeatingCalculation_V1))]
    public async Task Run([ActivityTrigger] ActivityInput input)
    {
        var orchestration = await _progressRepository.GetAsync(input.InstanceId).ConfigureAwait(false);
        var enqueueStep = orchestration.GetStep(EnqueueActorMessagesStep.EnqueueActorMessagesStepSequence);

        if (enqueueStep.Lifecycle.TerminationState == OrchestrationStepTerminationState.Skipped)
        {
            return;
        }

        enqueueStep.Lifecycle.TransitionToTerminated(_clock, OrchestrationStepTerminationState.Skipped);
        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    public record ActivityInput(OrchestrationInstanceId InstanceId);
}
