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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

/// <summary>
/// Command for starting an orchestration instance for a message.
/// Must be JSON serializable.
/// </summary>
/// <typeparam name="TInputParameterDto">Must be a serializable type.</typeparam>
public abstract record StartOrchestrationInstanceMessageCommand<TInputParameterDto> :
    StartOrchestrationInstanceCommand<ActorIdentityDto, TInputParameterDto>
        where TInputParameterDto : IInputParameterDto
{
    /// <summary>
    /// Construct command.
    /// </summary>
    /// <param name="operatingIdentity">Identity executing the command.</param>
    /// <param name="orchestrationDescriptionUniqueName">Uniquely identifies the orchestration description from which the
    /// orchestration instance should be created.</param>
    /// <param name="inputParameter">Contains the Durable Functions orchestration input parameter value.</param>
    /// <param name="idempotencyKey">
    /// A value used by the Process Manager to ensure idempotency for a message command.
    /// The producer of the <see cref="StartOrchestrationInstanceMessageCommand{TInputParameterDto}"/> should
    /// create a key that is unique per command.</param>
    /// <param name="actorMessageId">The id of the actor message that triggered the orchestration instance.</param>
    /// <param name="transactionId">The id of the transaction that triggered the orchestration instance.</param>
    /// <param name="meteringPointId">The id of the metering point for which the orchestration is operating.</param>
    public StartOrchestrationInstanceMessageCommand(
        ActorIdentityDto operatingIdentity,
        OrchestrationDescriptionUniqueNameDto orchestrationDescriptionUniqueName,
        TInputParameterDto inputParameter,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
            : base(operatingIdentity, orchestrationDescriptionUniqueName, inputParameter)
    {
        IdempotencyKey = idempotencyKey;
        ActorMessageId = actorMessageId;
        TransactionId = transactionId;
        MeteringPointId = meteringPointId;
    }

    /// <summary>
    /// A value used by the Process Manager to ensure idempotency for a message command.
    /// The producer of the <see cref="StartOrchestrationInstanceMessageCommand{TInputParameterDto}"/> should
    /// create a key that is unique per command.
    /// Max length is 1024 characters.
    /// </summary>
    public string IdempotencyKey { get; }

    /// <summary>
    /// The id of the actor message that triggered the orchestration instance.
    /// Max length is 36 characters.
    /// </summary>
    public string ActorMessageId { get; }

    /// <summary>
    /// The id of the transaction that triggered the orchestration instance.
    /// Max length is 36 characters.
    /// </summary>
    public string TransactionId { get; }

    /// <summary>
    /// The id of the metering point for which the orchestration is operating.
    /// Max length is 36 characters.
    /// </summary>
    public string? MeteringPointId { get; }
}
