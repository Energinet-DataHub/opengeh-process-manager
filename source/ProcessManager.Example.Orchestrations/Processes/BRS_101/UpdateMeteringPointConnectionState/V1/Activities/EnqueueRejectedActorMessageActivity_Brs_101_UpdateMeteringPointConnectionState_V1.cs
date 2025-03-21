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
using Energinet.DataHub.ProcessManager.Components.Abstractions.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Activities;

internal class EnqueueRejectedActorMessageActivity_Brs_101_UpdateMeteringPointConnectionState_V1(
    IOrchestrationInstanceProgressRepository repository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueRejectedActorMessageActivity_Brs_101_UpdateMeteringPointConnectionState_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<UpdateMeteringPointConnectionStateInputV1>();

        var rejectedMessageData = new UpdateMeteringPointConnectionStateRejectedV1(
            OriginalActorMessageId: orchestrationInstanceInput.ActorMessageId,
            OriginalTransactionId: orchestrationInstanceInput.TransactionId,
            RequestedByActorNumber: ActorNumber.Create(orchestrationInstanceInput.RequestedByActorNumber),
            RequestedByActorRole: ActorRole.FromName(orchestrationInstanceInput.RequestedByActorRole),
            ValidationErrors: input.ValidationErrors
                .Select(e => new ValidationErrorDto(
                    Message: e.Message,
                    ErrorCode: e.ErrorCode))
                .ToList());

        await _enqueueActorMessagesClient
            .EnqueueAsync(
                Orchestration_Brs_101_UpdateMeteringPointConnectionState_V1.UniqueName,
                orchestrationInstance.Id.Value,
                orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
                input.IdempotencyKey,
                rejectedMessageData)
            .ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        Guid IdempotencyKey,
        IReadOnlyCollection<ValidationError> ValidationErrors);
}
