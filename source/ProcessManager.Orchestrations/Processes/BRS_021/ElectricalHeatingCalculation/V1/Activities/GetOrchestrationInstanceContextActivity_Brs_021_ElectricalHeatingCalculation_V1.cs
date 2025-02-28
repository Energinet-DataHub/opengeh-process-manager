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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities;

/// <summary>
/// Get the <see cref="OrchestrationInstanceContext"/> for the orchestration instance.
/// </summary>
internal class GetOrchestrationInstanceContextActivity_Brs_021_ElectricalHeatingCalculation_V1(
    IOrchestrationInstanceProgressRepository repository,
    IOptions<OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1> orchestrationOptions)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1 _orchestrationOptions = orchestrationOptions.Value;

    [Function(nameof(GetOrchestrationInstanceContextActivity_Brs_021_ElectricalHeatingCalculation_V1))]
    public async Task<OrchestrationInstanceContext> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        return new OrchestrationInstanceContext(
            _orchestrationOptions,
            CalculationId: input.InstanceId.Value,
            input.InstanceId);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId);
}
