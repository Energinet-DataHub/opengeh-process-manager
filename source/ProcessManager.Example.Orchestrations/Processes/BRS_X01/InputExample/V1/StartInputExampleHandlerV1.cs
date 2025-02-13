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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Steps;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1;

internal class StartInputExampleHandlerV1(
    IStartOrchestrationInstanceCommands manager) :
        IStartOrchestrationInstanceCommandHandler<StartInputExampleCommandV1, InputV1>,
        IScheduleOrchestrationInstanceCommandHandler<ScheduleInputExampleCommandV1, InputV1>
{
    private readonly IStartOrchestrationInstanceCommands _manager = manager;

    public async Task<Guid> HandleAsync(StartInputExampleCommandV1 command)
    {
        // Here we show how its possible, based on input, to decide certain steps should be skipped by the orchestration.
        IReadOnlyCollection<int> skipStepsBySequence = command.InputParameter.ShouldSkipSkippableStep
            ? [SkippableStep.StepSequence]
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

        return orchestrationInstanceId.Value;
    }

    public async Task<Guid> HandleAsync(ScheduleInputExampleCommandV1 command)
    {
        // Here we show how its possible, based on input, to decide certain steps should be skipped by the orchestration.
        IReadOnlyCollection<int> skipStepsBySequence = command.InputParameter.ShouldSkipSkippableStep
            ? [SkippableStep.StepSequence]
            : [];

        var orchestrationInstanceId = await _manager
            .ScheduleNewOrchestrationInstanceAsync(
                identity: new UserIdentity(
                    new UserId(command.OperatingIdentity.UserId),
                    new ActorId(command.OperatingIdentity.ActorId)),
                uniqueName: new OrchestrationDescriptionUniqueName(
                    command.OrchestrationDescriptionUniqueName.Name,
                    command.OrchestrationDescriptionUniqueName.Version),
                inputParameter: command.InputParameter,
                runAt: command.RunAt.ToInstant(),
                skipStepsBySequence: skipStepsBySequence)
            .ConfigureAwait(false);

        return orchestrationInstanceId.Value;
    }
}
