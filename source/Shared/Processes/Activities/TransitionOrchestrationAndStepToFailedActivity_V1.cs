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

using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Shared.Processes.Activities;

/// <summary>
/// Transition the orchestration instance and one of it's steps to failed. Should be used to update
/// the orchestration instance when a step fails.
/// </summary>
internal class TransitionOrchestrationAndStepToFailedActivity_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository repository)
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;

    [Function(nameof(TransitionOrchestrationAndStepToFailedActivity_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var step = orchestrationInstance.GetStep(input.FailedStepSequence);
        step.Lifecycle.TransitionToTerminated(_clock, OrchestrationStepTerminationState.Failed);
        step.CustomState.SetFromInstance(new FailedStepCustomState(
            ErrorMessage: input.FailedStepErrorMessage));

        orchestrationInstance.Lifecycle.TransitionToFailed(_clock);

        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Information about what step failed and the error message.
    /// </summary>
    /// <param name="OrchestrationInstanceId">Id of the orchestration instance that failed.</param>
    /// <param name="FailedStepSequence">Sequence number of the step that failed.</param>
    /// <param name="FailedStepErrorMessage">Error message, typically the exception's <see cref="Exception.ToString()"/> output. Is set as the failed step's <see cref="StepInstance.CustomState"/>.</param>
    internal record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        int FailedStepSequence,
        string FailedStepErrorMessage);

    /// <summary>
    /// Failed step custom state object, which will be serialized to the step instance's custom state.
    /// </summary>
    internal record FailedStepCustomState(
        string ErrorMessage);
}
