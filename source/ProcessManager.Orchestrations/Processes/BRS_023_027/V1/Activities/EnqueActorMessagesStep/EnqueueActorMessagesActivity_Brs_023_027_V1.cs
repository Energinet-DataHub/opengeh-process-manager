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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.EnqueActorMessagesStep;

internal class EnqueueActorMessagesActivity_Brs_023_027_V1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueActorMessagesActivity_Brs_023_027_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        await _enqueueActorMessagesClient.Enqueue(
            orchestration: Orchestration_Brs_023_027_V1.UniqueName,
            orchestrationInstanceId: input.InstanceId.Value,
            orchestrationStartedBy: orchestrationInstance.Lifecycle.CreatedBy.Value.ToDto(),
            messageId: CreateIdempotencyKey(input.InstanceId, input.CalculatedData),
            data: input.CalculatedData).ConfigureAwait(false);
    }

    private string CreateIdempotencyKey(OrchestrationInstanceId orchestrationInstanceId, CalculatedDataForCalculationTypeV1 calculatedData)
    {
        return calculatedData.CalculationId.ToString() + orchestrationInstanceId.Value;
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        CalculatedDataForCalculationTypeV1 CalculatedData);
}
