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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.ActorRequestProcessExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.ActorRequestProcessExample.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.ActorRequestProcessExample.V1.Activities;

internal class EnqueueActorMessagesActivity_Brs_X02_ActorRequestProcessExample_V1(
    IOrchestrationInstanceProgressRepository repository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueActorMessagesActivity_Brs_X02_ActorRequestProcessExample_V1))]
    public async Task<string> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<ActorRequestProcessExampleInputV1>();

        var businessReason = BusinessReason.FromName(orchestrationInstanceInput.BusinessReason);

        await _enqueueActorMessagesClient.EnqueueAsync(
            Orchestration_Brs_X02_ActorRequestProcessExample_V1.UniqueName,
            orchestrationInstance.Id.Value,
            orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
            input.IdempotencyKey,
            new ActorRequestProcessExampleEnqueueDataV1(
                OriginalActorMessageId: orchestrationInstance.ActorMessageId?.Value
                    ?? throw new NullReferenceException($"ActorMessageId is null for orchestration instance with id {orchestrationInstance.Id.Value}"),
                ActorNumber.Create(orchestrationInstanceInput.RequestedByActorNumber),
                ActorRole.FromName(orchestrationInstanceInput.RequestedByActorRole),
                businessReason))
            .ConfigureAwait(false);

        return "Success";
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        Guid IdempotencyKey);
}
