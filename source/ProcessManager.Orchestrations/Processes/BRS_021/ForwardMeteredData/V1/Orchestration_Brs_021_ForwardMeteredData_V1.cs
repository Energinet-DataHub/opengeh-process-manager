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
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public class Orchestration_Brs_021_ForwardMeteredData_V1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock)
{
    internal const int ValidatingStep = 1;
    internal const int ForwardToMeasurementStep = 2;
    internal const int FindReceiverStep = 3;
    internal const int EnqueueActorMessagesStep = 4;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_021_ForwardedMeteredData.V1;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;

    /// <summary>
    /// Responsible for executing the initialization of the orchestration
    /// which includes the following steps:
    /// - fetching the metered data from Electricity Market
    /// - validating the incoming metered data
    /// - If the metered data is valid, the metered data is forwarded to Measurements
    /// - If the metered data is invalid, a validation error forwarded to EDI
    /// </summary>
    public async Task InitializeAsync(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // Initialize orchestration instance
        orchestrationInstance.Lifecycle.TransitionToRunning(_clock);
        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        // Start Step: Validate Metered Data
        await StartStep(orchestrationInstance, ValidatingStep);

        // Fetch Metered Data and store received data used to find receiver later in the orchestration
        // Validate Metered Data
        // Terminate Step: Validate Metered Data
        await TerminateStep(orchestrationInstance, ValidatingStep);

        // Start Step: Forward to Measurements
        await StartStep(orchestrationInstance, ForwardToMeasurementStep);
    }

    /// <summary>
    /// Responsible for forwarding the metered data after it has been validated and Measurements has confirmed
    /// they have received the data. The steps are as follows:
    /// - Find the market receiver of the metered data
    /// - Enqueue the metered data to the market receiver
    /// </summary>
    public async Task EnqueueMessages(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // Teriminate step: Forward to Measurements
        await TerminateStep(orchestrationInstance, ValidatingStep);

        // Start Step: Find Receiver
        await StartStep(orchestrationInstance, FindReceiverStep);
        // Find Receiver
        // Terminate Step: Find Receiver
        await TerminateStep(orchestrationInstance, FindReceiverStep);

        // Start Step: Enqueue Actor Messages
        await StartStep(orchestrationInstance, EnqueueActorMessagesStep);
        // Enqueue Actor Messages
    }

    /// <summary>
    /// Responsible for terminating the orchestration instance after
    /// EDI has enqueue the metered data to the market receiver.
    /// </summary>
    public async Task TerminateAsync(OrchestrationInstanceId orchestrationInstanceId)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // Terminate Step: Enqueue Actor Messages
        await TerminateStep(orchestrationInstance, EnqueueActorMessagesStep);

        // Terminate orchestration instance
        orchestrationInstance.Lifecycle.TransitionToSucceeded(_clock);
        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    private async Task TerminateStep(OrchestrationInstance orchestrationInstance, int step)
    {
        orchestrationInstance.TransitionStepToTerminated(
            step,
            OrchestrationStepTerminationState.Succeeded,
            _clock);

        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    private async Task StartStep(OrchestrationInstance orchestrationInstance, int step)
    {
        orchestrationInstance.TransitionStepToRunning(
            step,
            _clock);

        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }
}
