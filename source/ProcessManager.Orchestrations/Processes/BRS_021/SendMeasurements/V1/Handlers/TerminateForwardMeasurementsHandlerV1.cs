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
using Microsoft.ApplicationInsights;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Handlers;

public class TerminateForwardMeasurementsHandlerV1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock,
    TelemetryClient telemetryClient)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;
    private readonly TelemetryClient _telemetryClient = telemetryClient;

    public async Task HandleAsync(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // If the orchestration instance is terminated, do nothing (idempotency/retry check).
        if (orchestrationInstance.Lifecycle.State is OrchestrationInstanceLifecycleState.Terminated)
            return;

        // If we reach this point, the orchestration instance should be running, so this check is just an extra safeguard.
        if (orchestrationInstance.Lifecycle.State is not OrchestrationInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Orchestration instance must be running (Id={orchestrationInstance.Id}, State={orchestrationInstance.Lifecycle.State}).");

        await TerminateEnqueueActorMessagesStep(orchestrationInstance).ConfigureAwait(false);

        await TerminateOrchestrationInstance(orchestrationInstance).ConfigureAwait(false);
    }

    private async Task TerminateEnqueueActorMessagesStep(OrchestrationInstance orchestrationInstance)
    {
        var enqueueStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);

        // If the step is already terminated (idempotency/retry check), do nothing.
        if (enqueueStep.Lifecycle.State == StepInstanceLifecycleState.Terminated)
            return;

        // If we reach this point, the step should be running, so this check is just an extra safeguard.
        // The alternative is that the step is pending, but we shouldn't be able to receive a "notify" event
        // from measurements if the step hasn't transitioned to running yet.
        if (enqueueStep.Lifecycle.State is not StepInstanceLifecycleState.Running)
            throw new InvalidOperationException($"Enqueue actor messages step must be running (Id={enqueueStep.Id}, State={enqueueStep.Lifecycle.State}).");

        await StepHelper.TerminateStepAndCommit(enqueueStep, _clock, _progressRepository, _telemetryClient).ConfigureAwait(false);
    }

    private async Task TerminateOrchestrationInstance(OrchestrationInstance orchestrationInstance)
    {
        var businessValidationStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.BusinessValidationStep);

        var succeededBusinessValidation = businessValidationStep.Lifecycle is { TerminationState: StepInstanceTerminationState.Succeeded };
        if (succeededBusinessValidation)
            orchestrationInstance.Lifecycle.TransitionToSucceeded(_clock);
        else
            orchestrationInstance.Lifecycle.TransitionToFailed(_clock);

        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }
}
