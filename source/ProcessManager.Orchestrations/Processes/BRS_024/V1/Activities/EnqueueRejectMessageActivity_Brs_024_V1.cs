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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Activities;

public class EnqueueRejectMessageActivity_Brs_024_V1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueRejectMessageActivity_Brs_024_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<RequestYearlyMeasurementsInputV1>();

        await EnqueueRejectMessageAsync(
            orchestrationInstance.Lifecycle.CreatedBy.Value,
            input,
            orchestrationInstanceInput).ConfigureAwait(false);
    }

    private Task EnqueueRejectMessageAsync(
        OperatingIdentity orchestrationCreatedBy,
        ActivityInput input,
        RequestYearlyMeasurementsInputV1 requestInput)
    {
        var rejectedMessage = new RequestYearlyMeasurementsRejectV1(
            OriginalActorMessageId: requestInput.ActorMessageId,
            OriginalTransactionId: requestInput.TransactionId,
            RequestedForActorNumber: ActorNumber.Create(requestInput.ActorNumber),
            RequestedForActorRole: ActorRole.FromName(requestInput.ActorRole),
            BusinessReason: BusinessReason.FromName(requestInput.BusinessReason),
            ValidationErrors: input.ValidationErrors
                .Select(e => new ValidationErrorDto(
                    Message: e.Message,
                    ErrorCode: e.ErrorCode))
                .ToList());

        return _enqueueActorMessagesClient.EnqueueAsync(
            orchestration: Orchestration_Brs_024_V1.UniqueName,
            orchestrationInstanceId: input.InstanceId.Value,
            orchestrationStartedBy: orchestrationCreatedBy.MapToDto(),
            input.IdempotencyKey,
            data: rejectedMessage);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        IReadOnlyCollection<ValidationError> ValidationErrors,
        Guid IdempotencyKey);
}
