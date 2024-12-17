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

using Azure.Messaging.ServiceBus;
using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Shared;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1;

public class RequestCalculatedEnergyTimeSeriesHandlerV1(
    ILogger<RequestCalculatedEnergyTimeSeriesHandlerV1> logger,
    IStartOrchestrationInstanceCommands commands)
    : StartOrchestrationFromMessageHandlerBase<RequestCalculatedEnergyTimeSeriesInputV1>(logger)
{
    private readonly IStartOrchestrationInstanceCommands _commands = commands;

    protected override async Task StartOrchestration(ActorIdentity actorIdentity, RequestCalculatedEnergyTimeSeriesInputV1 input)
    {
        var orchestrationDescriptionUniqueName = new Brs_026_V1();

        await _commands.StartNewOrchestrationInstanceAsync(
                identity: actorIdentity,
                uniqueName: new OrchestrationDescriptionUniqueName(
                    orchestrationDescriptionUniqueName.Name,
                    orchestrationDescriptionUniqueName.Version),
                input,
                [])
            .ConfigureAwait(false);
    }
}
