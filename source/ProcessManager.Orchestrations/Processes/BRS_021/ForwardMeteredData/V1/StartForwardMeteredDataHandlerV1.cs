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

using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public class StartForwardMeteredDataHandlerV1(
    ILogger<StartForwardMeteredDataHandlerV1> logger,
    IStartOrchestrationInstanceMessageCommands commands,
    Orchestration_Brs_021_ForwardMeteredData_V1 orchestration) : StartOrchestrationInstanceFromMessageHandlerBase<MeteredDataForMeteringPointMessageInputV1>(logger)
{
    private const int ValidatingStep = 1;
    private const int ForwardMeteredDataStep = 2;
    private readonly IStartOrchestrationInstanceMessageCommands _commands = commands;
    private readonly Orchestration_Brs_021_ForwardMeteredData_V1 _orchestration = orchestration;

    protected override async Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        MeteredDataForMeteringPointMessageInputV1 input,
        string idempotencyKey)
    {
        // Start the orchestration instance
        // TODO: should not trigger a durable function.
        var orchestrationInstanceId = await _commands.StartNewOrchestrationInstanceAsync(
                actorIdentity,
                OrchestrationDescriptionUniqueName.FromDto(Orchestration_Brs_021_ForwardMeteredData_V1.UniqueName),
                input,
                skipStepsBySequence: [],
                new IdempotencyKey(idempotencyKey))
            .ConfigureAwait(false);

        await _orchestration.ReceiverMeteredData(input, orchestrationInstanceId).ConfigureAwait(false);
    }
}
