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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Options;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities;

/// <summary>
/// The first activity in the orchestration.
/// It is responsible for updating the status to 'Running' and return
/// key information needed to configure, plan and handle the orchestration execution.
/// </summary>
internal class OrchestrationInitializeActivity_Brs_023_027_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository,
    IOptions<OrchestrationOptions_Brs_023_027_V1> orchestrationOptions)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    private OrchestrationOptions_Brs_023_027_V1 OrchestrationOptions { get; } = orchestrationOptions.Value;

    [Function(nameof(OrchestrationInitializeActivity_Brs_023_027_V1))]
    public async Task<OrchestrationExecutionContext> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationOptions = OrchestrationOptions;

        var orchestrationInstance = await ProgressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        orchestrationInstance.Lifecycle.TransitionToRunning(Clock);
        await ProgressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        // Calculation must have been started by a User identity, so we know we can cast to it here
        var userIdentityDto = (UserIdentityDto)orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto();

        var stepsSkippedBySequence = orchestrationInstance.Steps
            .Where(step => step.IsSkipped())
            .Select(step => step.Sequence)
        .ToList();

        return new OrchestrationExecutionContext(
            orchestrationOptions,
            userIdentityDto.UserId,
            userIdentityDto.ActorId,
            stepsSkippedBySequence);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId);
}
