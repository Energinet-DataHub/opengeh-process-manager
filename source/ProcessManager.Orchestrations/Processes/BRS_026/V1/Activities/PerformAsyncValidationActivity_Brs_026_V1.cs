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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.Activities;

/// <summary>
/// Perform async validation (and set step to running)
/// </summary>
internal class PerformAsyncValidationActivity_Brs_026_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository)
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;

    public static Task<bool> RunActivity(TaskOrchestrationContext context, AsyncValidationActivityInput activityInput, TaskOptions options)
    {
        return context.CallActivityAsync<bool>(
            nameof(PerformAsyncValidationActivity_Brs_026_V1),
            activityInput,
            options);
    }

    [Function(nameof(PerformAsyncValidationActivity_Brs_026_V1))]
    public async Task<bool> Run(
        [ActivityTrigger] AsyncValidationActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        orchestrationInstance.TransitionStepToRunning(
            Orchestration_RequestCalculatedEnergyTimeSeries_V1.AsyncValidationStepSequence,
            _clock);
        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        var isValid = await PerformAsyncValidationAsync(input.RequestInput).ConfigureAwait(false);

        return isValid;
    }

    private async Task<bool> PerformAsyncValidationAsync(RequestCalculatedEnergyTimeSeriesInputV1 requestInput)
    {
        // TODO: Perform async validation instead of delay
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        return true;
    }

    public record AsyncValidationActivityInput(
        OrchestrationInstanceId InstanceId,
        RequestCalculatedEnergyTimeSeriesInputV1 RequestInput);
}
