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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public class Orchestration_Brs_021_ForwardMeteredData_V1(
    IOrchestrationInstanceProgressRepository repository,
    IClock clock)
{
    internal const int ValidatingStep = 1;
    internal const int ForwardMeteredDataStep = 2;
    internal const int FindReceiverStep = 3;
    internal const int EnqueueActorMessagesStep = 4;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_021_ForwardedMeteredData.V1;
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IClock _clock = clock;

    // [Function(nameof(Orchestration_Brs_021_ForwardMeteredData_V1))]
    public async Task ReceiverMeteredData(MeteredDataForMeteringPointMessageInputV1 input, OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _repository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // Transition the orchestration instance to the 'Running' state
        await TransitionOrchestrationInstancesToRunningAsync(orchestrationInstance).ConfigureAwait(false);

        // start step: Validating metered data
        await TransitionStepToRunningActivityAsync(orchestrationInstance, ValidatingStep).ConfigureAwait(false);
        // fetch master data for metering point
        // business validation
        // terminate step: validation
        await TransitionStepToTerminatedActivityAsync(orchestrationInstance, ValidatingStep).ConfigureAwait(false);

        // start step: Forwarding metered data
        await TransitionStepToRunningActivityAsync(orchestrationInstance, ForwardMeteredDataStep).ConfigureAwait(false);
        // store incoming metered data + neighbor grid area owner´, etc.
        // send metered data to measurements
    }

    public async Task ForwardMeteredData(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _repository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // terminate step: Forwarding metered data
        await TransitionStepToTerminatedActivityAsync(orchestrationInstance, ForwardMeteredDataStep).ConfigureAwait(false);

        // start step: Find Receivers
        await TransitionStepToRunningActivityAsync(orchestrationInstance, FindReceiverStep).ConfigureAwait(false);
        // fetch incoming metered data + neighbor grid area owner´, etc.
        // find all receivers for the metered data
        // terminate step: Find Receivers
        await TransitionStepToTerminatedActivityAsync(orchestrationInstance, FindReceiverStep).ConfigureAwait(false);

        // start step: Enqueue Actor Messages
        await TransitionStepToRunningActivityAsync(orchestrationInstance, EnqueueActorMessagesStep).ConfigureAwait(false);
        // send metered data to all receivers
    }

    public async Task MessagesEnqueued(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _repository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // terminate step: Enqueue Actor Messages
        await TransitionStepToTerminatedActivityAsync(orchestrationInstance, EnqueueActorMessagesStep).ConfigureAwait(false);

        // Transition the orchestration instance to the 'Succeeded' state
        await TransitionOrchestrationInstancesToSucceededAsync(orchestrationInstance).ConfigureAwait(false);
    }

    private async Task TransitionOrchestrationInstancesToSucceededAsync(OrchestrationInstance orchestrationInstance)
    {
        orchestrationInstance.Lifecycle.TransitionToSucceeded(_clock);
        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    private async Task TransitionOrchestrationInstancesToRunningAsync(OrchestrationInstance orchestrationInstance)
    {
        orchestrationInstance.Lifecycle.TransitionToRunning(_clock);
        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    private async Task TransitionStepToRunningActivityAsync(OrchestrationInstance orchestrationInstance, int stepSequence)
    {
        orchestrationInstance.TransitionStepToRunning(
            stepSequence,
            _clock);

        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    private async Task TransitionStepToTerminatedActivityAsync(OrchestrationInstance orchestrationInstance, int stepSequence)
    {
        orchestrationInstance.TransitionStepToTerminated(
            stepSequence,
            OrchestrationStepTerminationState.Succeeded,
            _clock);

        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }
}
