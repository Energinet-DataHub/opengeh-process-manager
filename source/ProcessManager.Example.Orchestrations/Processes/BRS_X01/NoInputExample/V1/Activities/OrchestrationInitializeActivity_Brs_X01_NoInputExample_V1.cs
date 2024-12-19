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

using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Model;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Activities;

internal class OrchestrationInitializeActivity_Brs_X01_NoInputExample_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    [Function(nameof(OrchestrationInitializeActivity_Brs_X01_NoInputExample_V1))]
    public async Task<OrchestrationExecutionPlan> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await ProgressRepository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        orchestrationInstance.Lifecycle.TransitionToRunning(Clock);
        await ProgressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        // Orchestrations that have input have a custom start handler in which they can
        // transition steps to skipped before starting the orchestration.
        // Orchestrations without input doesn't have a custom start handler, so if they
        // want to skip steps it must be done dynamically during the orchestration.
        // Here we just use a random function to determine if skippable functions should be skipped or not.
        var shouldSkip = Random.Shared.Next(0, 2) == 0;
        var stepsSkippedBySequence = orchestrationInstance.Steps
            .Where(step => step.Lifecycle.CanBeSkipped && shouldSkip)
            .Select(step => step.Sequence)
            .ToList();
        return new OrchestrationExecutionPlan(stepsSkippedBySequence);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);
}
