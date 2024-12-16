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
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.Activities;

/// <summary>
/// Set the orchestration instance lifecycle to terminated
/// </summary>
internal class TerminateOrchestrationActivity_Brs_026_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository)
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;

    public static Task RunActivity(TaskOrchestrationContext context, TerminateOrchestrationActivityInput input, TaskOptions options)
    {
        return context.CallActivityAsync(
            nameof(TerminateOrchestrationActivity_Brs_026_V1),
            input,
            options);
    }

    [Function(nameof(TerminateOrchestrationActivity_Brs_026_V1))]
    public async Task Run(
        [ActivityTrigger] TerminateOrchestrationActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        switch (input.TerminationState)
        {
            case OrchestrationInstanceTerminationStates.Succeeded:
                orchestrationInstance.Lifecycle.TransitionToSucceeded(_clock);
                break;

            case OrchestrationInstanceTerminationStates.Failed:
                orchestrationInstance.Lifecycle.TransitionToFailed(_clock);
                break;

            case OrchestrationInstanceTerminationStates.UserCanceled:
            default:
                throw new ArgumentOutOfRangeException(nameof(input.TerminationState), input.TerminationState, "Invalid termination state");
        }

        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    public record TerminateOrchestrationActivityInput(
        OrchestrationInstanceId InstanceId,
        OrchestrationInstanceTerminationStates TerminationState);
}
