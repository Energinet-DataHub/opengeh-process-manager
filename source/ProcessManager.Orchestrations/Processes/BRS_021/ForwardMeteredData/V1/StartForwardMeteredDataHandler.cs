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
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public class StartForwardMeteredDataHandler(IStartOrchestrationInstanceCommands commands)
{
    private readonly IStartOrchestrationInstanceCommands _commands = commands;

    /// <summary>
    /// Start forward metered data.
    /// </summary>
    public async Task StartForwardMeteredDataAsync(MeteredDataForMeasurementPointMessageInputV1 input)
    {
        var orchestrationDescriptionUniqueName = new Brs_021_ForwardedMeteredData_V1();

        await _commands.StartNewOrchestrationInstanceAsync(
                identity: new ActorIdentity(new ActorId(input.AuthenticatedActorId)), // TODO: Any call to commands must include identity information; see 'ScheduleOrchestrationInstanceCommand' and 'CancelScheduledOrchestrationInstanceCommand'
                uniqueName: new OrchestrationDescriptionUniqueName(
                    orchestrationDescriptionUniqueName.Name,
                    orchestrationDescriptionUniqueName.Version),
                input,
                [])
            .ConfigureAwait(false);
    }
}
