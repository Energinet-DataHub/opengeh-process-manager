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

using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

internal class EnqueueMessagesActivity_Brs_021_ForwardMeteredData_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    [Function(nameof(EnqueueMessagesActivity_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [ActivityTrigger] Guid orchestrationInstanceId)
    {
        var orchestrationInstance = await ProgressRepository
            .GetAsync(new OrchestrationInstanceId(orchestrationInstanceId))
            .ConfigureAwait(false);

        var step = orchestrationInstance.Steps.Single(x => x.Sequence == Orchestration_Brs_021_ForwardMeteredData_V1.EnqueueMessagesStep);
        step.Lifecycle.TransitionToRunning(Clock);
        await ProgressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        // TODO: For demo purposes; remove when done
        await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
    }
}