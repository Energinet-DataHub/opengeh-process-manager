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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities;

/// <summary>
/// Get the <see cref="OrchestrationInstanceContext"/> for the orchestration instance.
/// </summary>
internal class GetOrchestrationInstanceContextActivity_Brs_023_027_V1(
    IOrchestrationInstanceProgressRepository repository,
    IOptions<OrchestrationOptions_Brs_023_027_V1> orchestrationOptions)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly OrchestrationOptions_Brs_023_027_V1 _orchestrationOptions = orchestrationOptions.Value;

    [Function(nameof(GetOrchestrationInstanceContextActivity_Brs_023_027_V1))]
    public async Task<OrchestrationInstanceContext> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        // Calculation must have been started by a User identity, so we know we can cast to it here
        var userIdentityDto = (UserIdentityDto)orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto();

        var stepsSkippedBySequence = orchestrationInstance.Steps
            .Where(step => step.IsSkipped())
            .Select(step => step.Sequence)
        .ToList();

        orchestrationInstance.CustomState.

        return new OrchestrationInstanceContext(
            OrchestrationOptions: _orchestrationOptions,
            CalculationId: input.InstanceId.Value,
            UserId: userIdentityDto.UserId,
            OrchestrationInstanceId: input.InstanceId,
            SkippedStepsBySequence: stepsSkippedBySequence);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId);
}
