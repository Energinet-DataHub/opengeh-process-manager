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
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Activities;

/// <summary>
/// Enqueue reject message in EDI (and set step to running)
/// </summary>
internal class EnqueueRejectMessageActivity_Brs_028_V1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueRejectMessageActivity_Brs_028_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<RequestCalculatedWholesaleServicesInputV1>();

        await EnqueueRejectMessageAsync(
            orchestrationInstance.Lifecycle.CreatedBy.Value,
            input,
            orchestrationInstanceInput).ConfigureAwait(false);
    }

    private Task EnqueueRejectMessageAsync(
        OperatingIdentity orchestrationCreatedBy,
        ActivityInput input,
        RequestCalculatedWholesaleServicesInputV1 requestInput)
    {
        var rejectedMessage = new RequestCalculatedWholesaleServicesRejectedV1(
            OriginalActorMessageId: requestInput.ActorMessageId,
            OriginalTransactionId: requestInput.TransactionId,
            RequestedForActorNumber: ActorNumber.Create(requestInput.RequestedForActorNumber),
            RequestedForActorRole: ActorRole.FromName(requestInput.RequestedForActorRole),
            RequestedByActorNumber: ActorNumber.Create(requestInput.RequestedByActorNumber),
            RequestedByActorRole: ActorRole.FromName(requestInput.RequestedByActorRole),
            BusinessReason: BusinessReason.FromName(requestInput.BusinessReason),
            ValidationErrors: input.ValidationErrors
                .Select(e => new ValidationErrorDto(
                    Message: e.Message,
                    ErrorCode: e.ErrorCode))
                .ToList());

        return _enqueueActorMessagesClient.EnqueueAsync(
            Orchestration_Brs_028_V1.UniqueName,
            input.InstanceId.Value,
            orchestrationCreatedBy.MapToDto(),
            input.IdempotencyKey,
            rejectedMessage);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        IReadOnlyCollection<ValidationError> ValidationErrors,
        Guid IdempotencyKey);
}
