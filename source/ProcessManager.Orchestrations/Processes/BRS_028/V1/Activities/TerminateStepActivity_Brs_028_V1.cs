﻿// Copyright 2020 Energinet DataHub A/S
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
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_028.V1.Activities;

/// <summary>
/// Set the orchestration instance step lifecycle to terminated
/// </summary>
internal class TerminateStepActivity_Brs_028_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository)
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;

    [Function(nameof(TerminateStepActivity_Brs_028_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        orchestrationInstance.TransitionStepToTerminated(
            input.StepSequence,
            input.TerminationState,
            _clock);

        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        int StepSequence,
        OrchestrationStepTerminationStates TerminationState);
}