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

using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

/// <summary>
/// Enqueue reject message in EDI (and set step to running)
/// </summary>
internal class EnqueueRejectMessageActivity_Brs_021_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueRejectMessageActivity_Brs_021_V1))]
    public async Task Run([ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        orchestrationInstance.TransitionStepToRunning(
            Orchestration_Brs_021_ForwardMeteredData_V1.EnqueueActorMessagesStep,
            _clock);

        await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        await EnqueueRejectMessageAsync(orchestrationInstance.Lifecycle.CreatedBy.Value, input).ConfigureAwait(false);
    }

    private Task EnqueueRejectMessageAsync(OperatingIdentity orchestrationCreatedBy, ActivityInput input) =>
        _enqueueActorMessagesClient.Enqueue(
            Orchestration_Brs_021_ForwardMeteredData_V1.UniqueName,
            input.InstanceId.Value,
            orchestrationCreatedBy.ToDto(),
            "enqueue-" + input.InstanceId.Value,
            input.RejectMessage);

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        MeteredDataForMeasurementPointRejectedV1 RejectMessage);
}
