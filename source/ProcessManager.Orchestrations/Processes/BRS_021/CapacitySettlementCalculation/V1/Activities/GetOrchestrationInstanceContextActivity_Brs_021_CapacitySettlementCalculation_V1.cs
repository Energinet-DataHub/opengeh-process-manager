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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.CapacitySettlementCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.CapacitySettlementCalculation.V1.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.CapacitySettlementCalculation.V1.Activities;

/// <summary>
/// Get the <see cref="OrchestrationInstanceContext"/> for the orchestration instance.
/// </summary>
internal class GetOrchestrationInstanceContextActivity_Brs_021_CapacitySettlementCalculation_V1(
    IOptions<OrchestrationOptions_Brs_021_CapacitySettlementCalculation_V1> orchestrationOptions)
{
    private readonly OrchestrationOptions_Brs_021_CapacitySettlementCalculation_V1 _orchestrationOptions = orchestrationOptions.Value;

    [Function(nameof(GetOrchestrationInstanceContextActivity_Brs_021_CapacitySettlementCalculation_V1))]
    public Task<OrchestrationInstanceContext> Run(
        [ActivityTrigger] ActivityInput input)
    {
        // Orchestration options are added to storage in order have them available later and in the orchestration history.
        // The idea is to include the options early in order 1) to have them available and 2) to use the correct values.
        return Task.FromResult(new OrchestrationInstanceContext(
            _orchestrationOptions,
            input.InstanceId));
    }

    public record ActivityInput(OrchestrationInstanceId InstanceId);
}
