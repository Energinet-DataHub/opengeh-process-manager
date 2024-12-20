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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Model;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Activities;

internal class OrchestrationInitializeActivity_Brs_X01_InputExample_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    [Function(nameof(OrchestrationInitializeActivity_Brs_X01_InputExample_V1))]
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
        // We can extract that information and use it to plan the execution of the orchestrations.
        var stepsSkippedBySequence = orchestrationInstance.Steps
            .Where(step => step.IsSkipped())
            .Select(step => step.Sequence)
            .ToList();
        return new OrchestrationExecutionPlan(stepsSkippedBySequence);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);
}
