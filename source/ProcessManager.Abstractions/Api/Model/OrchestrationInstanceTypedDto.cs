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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

/// <summary>
/// Contains information about an orchestration instance.
/// Must be JSON serializable.
/// </summary>
public record OrchestrationInstanceTypedDto
{
    /// <summary>
    /// Construct DTO.
    /// </summary>
    /// <param name="id">The id of the orchestration instance.</param>
    /// <param name="lifecycle">The lifecycle state of the orchestration instance.</param>
    /// <param name="steps">The steps of the orchestration instance.</param>
    /// <param name="customState">Any custom state of the orchestration instance.</param>
    /// <param name="idempotencyKey">A value used by the Process Manager to ensure idempotency for a message command.</param>
    /// <param name="actorMessageId"></param>
    /// <param name="transactionId"></param>
    /// <param name="meteringPointId"></param>
    public OrchestrationInstanceTypedDto(
        Guid id,
        OrchestrationInstanceLifecycleDto lifecycle,
        IReadOnlyCollection<StepInstanceDto> steps,
        string customState,
        string? idempotencyKey = default,
        string? actorMessageId = default,
        string? transactionId = default,
        string? meteringPointId = default)
    {
        Id = id;
        Lifecycle = lifecycle;
        Steps = steps;
        CustomState = customState;
        IdempotencyKey = idempotencyKey;
        ActorMessageId = actorMessageId;
        TransactionId = transactionId;
        MeteringPointId = meteringPointId;
    }

    /// <summary>
    /// The id of the orchestration instance.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The lifecycle state of the orchestration instance.
    /// </summary>
    public OrchestrationInstanceLifecycleDto Lifecycle { get; }

    /// <summary>
    /// The steps of the orchestration instance.
    /// </summary>
    public IReadOnlyCollection<StepInstanceDto> Steps { get; }

    /// <summary>
    /// Any custom state of the orchestration instance.
    /// </summary>
    public string CustomState { get; }

    /// <summary>
    /// A value used by the Process Manager to ensure idempotency for a message command.
    /// </summary>
    public string? IdempotencyKey { get; }

    public string? ActorMessageId { get; }

    public string? TransactionId { get; }

    public string? MeteringPointId { get; }
}

/// <summary>
/// Contains information about an orchestration instance including
/// specific input parameter values.
/// Must be JSON serializable.
/// </summary>
/// <typeparam name="TInputParameterDto">Must be a JSON serializable type.</typeparam>
public record OrchestrationInstanceTypedDto<TInputParameterDto> :
    OrchestrationInstanceTypedDto,
    IOrchestrationInstanceTypedDto<TInputParameterDto>
        where TInputParameterDto : class, IInputParameterDto
{
    /// <summary>
    /// Construct DTO.
    /// </summary>
    /// <param name="id">The id of the orchestration instance.</param>
    /// <param name="lifecycle">The lifecycle state of the orchestration instance.</param>
    /// <param name="steps">The steps of the orchestration instance.</param>
    /// <param name="customState">Any custom state of the orchestration instance.</param>
    /// <param name="parameterValue">Contains the Durable Functions orchestration input parameter value.</param>
    /// <param name="idempotencyKey">A value used by the Process Manager to ensure idempotency for a message command.</param>
    /// <param name="actorMessageId"></param>
    /// <param name="transactionId"></param>
    /// <param name="meteringPointId"></param>
    public OrchestrationInstanceTypedDto(
        Guid id,
        OrchestrationInstanceLifecycleDto lifecycle,
        IReadOnlyCollection<StepInstanceDto> steps,
        string customState,
        TInputParameterDto parameterValue,
        string? idempotencyKey = default,
        string? actorMessageId = default,
        string? transactionId = default,
        string? meteringPointId = default)
        : base(id, lifecycle, steps, customState, idempotencyKey, actorMessageId, transactionId, meteringPointId)
    {
        ParameterValue = parameterValue;
    }

    /// <summary>
    /// Contains the Durable Functions orchestration input parameter value.
    /// </summary>
    public TInputParameterDto ParameterValue { get; }
}
