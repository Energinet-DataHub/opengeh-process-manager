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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.SubsystemTests.Processes.Shared;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_021.ElectricalHeatingCalculation.V1;

public class ElectricalHeatingCalculationScenarioState(
    StartElectricalHeatingCalculationCommandV1 startCommand) : IScenarioState
{
    public StartOrchestrationInstanceCommand<UserIdentityDto> StartCommand { get; set; } = startCommand;

    public OrchestrationInstanceTypedDto? OrchestrationInstance { get; set; }

    public Guid OrchestrationInstanceId { get; set; }
}
