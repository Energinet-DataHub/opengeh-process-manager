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

using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Orchestration.Steps;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Activities;

internal class SetStepCustomStateActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1(
    IOrchestrationInstanceProgressRepository repository,
    IClock clock)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IClock _clock = clock;

    [Function(nameof(SetStepCustomStateActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var stepInstance = orchestrationInstance.GetStep(input.StepSequence);
        stepInstance.CustomState.SetFromInstance(new WaitForNotifyEventStep.CustomState(
            Message: input.CustomStateMessage));

        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        int StepSequence,
        string CustomStateMessage);
}
