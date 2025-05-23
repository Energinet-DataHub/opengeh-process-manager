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
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Shared.Processes.Activities;

internal class TransitionStepToRunningActivity_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository repository)
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;

    [Function(nameof(TransitionStepToRunningActivity_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        orchestrationInstance.TransitionStepToRunning(
            input.StepSequence,
            _clock);

        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    internal record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        int StepSequence);
}
