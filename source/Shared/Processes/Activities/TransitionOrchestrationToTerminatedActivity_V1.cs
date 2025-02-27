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

internal class TransitionOrchestrationToTerminatedActivity_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository repository)
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;

    [Function(nameof(TransitionOrchestrationToTerminatedActivity_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        switch (input.TerminationState)
        {
            case OrchestrationInstanceTerminationState.Succeeded:
                orchestrationInstance.Lifecycle.TransitionToSucceeded(_clock);
                break;

            case OrchestrationInstanceTerminationState.Failed:
                orchestrationInstance.Lifecycle.TransitionToFailed(_clock);
                break;

            case OrchestrationInstanceTerminationState.UserCanceled:
            default:
                throw new ArgumentOutOfRangeException(nameof(input.TerminationState), input.TerminationState, "Invalid termination state");
        }

        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    internal record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        OrchestrationInstanceTerminationState TerminationState);
}
