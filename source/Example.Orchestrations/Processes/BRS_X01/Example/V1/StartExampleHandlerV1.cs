﻿// Copyright 2020 Energinet DataHub A/S
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

using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_X01.Example.V1.Model;
using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;

namespace Energinet.DataHub.Example.Orchestrations.Processes.BRS_X01.Example.V1;

internal class StartExampleHandlerV1(
    IStartOrchestrationInstanceCommands manager)
{
    private readonly IStartOrchestrationInstanceCommands _manager = manager;

    public async Task<OrchestrationInstanceId> StartNewCalculationAsync(StartExampleCommandV1 command)
    {
        // Here we show how its possible, based on input, to decide certain steps should be skipped by the orchestration.
        IReadOnlyCollection<int> skipStepsBySequence = command.InputParameter.SkipStepTwo
            ? [Orchestration_Brs_X01_Example_V1.SkippableStepSequence]
            : [];

        var orchestrationInstanceId = await _manager
            .StartNewOrchestrationInstanceAsync(
                identity: new UserIdentity(
                        new UserId(command.OperatingIdentity.UserId),
                        new ActorId(command.OperatingIdentity.ActorId)),
                uniqueName: new OrchestrationDescriptionUniqueName(
                    command.OrchestrationDescriptionUniqueName.Name,
                    command.OrchestrationDescriptionUniqueName.Version),
                inputParameter: command.InputParameter,
                skipStepsBySequence: skipStepsBySequence)
            .ConfigureAwait(false);

        return orchestrationInstanceId;
    }
}