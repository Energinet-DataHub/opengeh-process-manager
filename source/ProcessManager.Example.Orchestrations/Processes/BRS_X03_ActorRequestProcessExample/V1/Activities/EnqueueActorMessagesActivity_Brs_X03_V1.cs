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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample.V1;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1.Activities;

public class EnqueueActorMessagesActivity_Brs_X03_V1(
    IOrchestrationInstanceProgressRepository repository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueActorMessagesActivity_Brs_X03_V1))]
    public async Task<string> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository.GetAsync(input.OrchestrationInstanceId).ConfigureAwait(false);

        await _enqueueActorMessagesClient.EnqueueAsync(
            Orchestration_Brs_X03_V1.UniqueName,
            orchestrationInstance.Id.Value,
            orchestrationInstance.Lifecycle.CreatedBy.Value.ToDto(),
            input.IdempotencyKey.ToString(),
            new ActorRequestProcessExampleEnqueueDataV1(
                input.RequestedByActorNumber,
                input.RequestedByActorRole,
                input.BusinessReason))
            .ConfigureAwait(false);

        return "Success";
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        Guid IdempotencyKey,
        string RequestedByActorNumber,
        string RequestedByActorRole,
        string BusinessReason);
}
