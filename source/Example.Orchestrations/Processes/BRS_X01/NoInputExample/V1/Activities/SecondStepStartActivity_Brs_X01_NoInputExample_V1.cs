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
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Activities;

internal class SecondStepStartActivity_Brs_X01_NoInputExample_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    [Function(nameof(SecondStepStartActivity_Brs_X01_NoInputExample_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await ProgressRepository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var step = orchestrationInstance.Steps
            .Single(step => step.Sequence == Orchestration_Brs_X01_NoInputExample_V1.SkippableStepSequence);

        step.Lifecycle.TransitionToRunning(Clock);
        await ProgressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);
}
