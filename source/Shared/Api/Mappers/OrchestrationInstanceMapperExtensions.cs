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

using ApiModel = Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using DomainModel = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Shared.Api.Mappers;

#pragma warning disable SA1118 // Parameter should not span multiple lines
internal static class OrchestrationInstanceMapperExtensions
{
    public static ApiModel.OrchestrationInstanceTypedDto<TInputParameterDto> MapToTypedDto<TInputParameterDto>(
        this DomainModel.OrchestrationInstance entity)
            where TInputParameterDto : class, ApiModel.IInputParameterDto
    {
        return new ApiModel.OrchestrationInstanceTypedDto<TInputParameterDto>(
            entity.Id.Value,
            entity.Lifecycle.MapToDto(),
            entity.Steps.Select(step => step.MapToDto()).ToList(),
            entity.CustomState.Value,
            entity.ParameterValue.AsType<TInputParameterDto>(),
            entity.IdempotencyKey?.Value,
            entity.ActorMessageId?.Value,
            entity.TransactionId?.Value,
            entity.MeteringPointId?.Value);
    }

    public static ApiModel.OrchestrationInstance.OrchestrationInstanceDto MapToDto(
        this DomainModel.OrchestrationInstance entity)
    {
        return new ApiModel.OrchestrationInstance.OrchestrationInstanceDto(
            Id: entity.Id.Value,
            Lifecycle: entity.Lifecycle.MapToDto(),
            ParameterValue: entity.ParameterValue.AsExpandoObject(),
            Steps: entity.Steps.Select(step => step.MapToDto()).ToList(),
            CustomState: entity.CustomState.Value,
            IdempotencyKey: entity.IdempotencyKey?.Value,
            ActorMessageId: entity.ActorMessageId?.Value,
            TransactionId: entity.TransactionId?.Value,
            MeteringPointId: entity.MeteringPointId?.Value);
    }

    public static ApiModel.OrchestrationInstance.OrchestrationInstanceLifecycleDto MapToDto(
        this DomainModel.OrchestrationInstanceLifecycle entity)
    {
        return new ApiModel.OrchestrationInstance.OrchestrationInstanceLifecycleDto(
            CreatedBy: entity.CreatedBy.Value.MapToDto(),
            State: Enum
                .TryParse<ApiModel.OrchestrationInstance.OrchestrationInstanceLifecycleState>(
                    entity.State.ToString(),
                    ignoreCase: true,
                    out var lifecycleStateResult)
                ? lifecycleStateResult
                : throw new InvalidOperationException($"Invalid State '{entity.State}'; cannot be mapped."),
            TerminationState: Enum
                .TryParse<ApiModel.OrchestrationInstance.OrchestrationInstanceTerminationState>(
                    entity.TerminationState.ToString(),
                    ignoreCase: true,
                    out var terminationStateResult)
                ? terminationStateResult
                : null,
            CanceledBy: entity.CanceledBy?.Value.MapToDto(),
            CreatedAt: entity.CreatedAt.ToDateTimeOffset(),
            ScheduledToRunAt: entity.ScheduledToRunAt?.ToDateTimeOffset(),
            QueuedAt: entity.QueuedAt?.ToDateTimeOffset(),
            StartedAt: entity.StartedAt?.ToDateTimeOffset(),
            TerminatedAt: entity.TerminatedAt?.ToDateTimeOffset());
    }

    public static ApiModel.OrchestrationInstance.IOperatingIdentityDto MapToDto(
        this DomainModel.OperatingIdentity entity)
    {
        return entity.ToDto();
    }

    public static ApiModel.OrchestrationInstance.StepInstanceDto MapToDto(
        this DomainModel.StepInstance entity)
    {
        return new ApiModel.OrchestrationInstance.StepInstanceDto(
            Id: entity.Id.Value,
            Lifecycle: entity.Lifecycle.MapToDto(),
            Description: entity.Description,
            Sequence: entity.Sequence,
            CustomState: entity.CustomState.Value);
    }

    public static ApiModel.OrchestrationInstance.StepInstanceLifecycleDto MapToDto(
        this DomainModel.StepInstanceLifecycle entity)
    {
        return new ApiModel.OrchestrationInstance.StepInstanceLifecycleDto(
            State: Enum
                .TryParse<ApiModel.OrchestrationInstance.StepInstanceLifecycleState>(
                    entity.State.ToString(),
                    ignoreCase: true,
                    out var lifecycleStateResult)
                ? lifecycleStateResult
                : throw new InvalidOperationException($"Invalid State '{entity.State}'; cannot be mapped."),
            TerminationState: Enum
                .TryParse<ApiModel.OrchestrationInstance.OrchestrationStepTerminationState>(
                    entity.TerminationState.ToString(),
                    ignoreCase: true,
                    out var terminationStateResult)
                ? terminationStateResult
                : null,
            StartedAt: entity.StartedAt?.ToDateTimeOffset(),
            TerminatedAt: entity.TerminatedAt?.ToDateTimeOffset());
    }

    public static IReadOnlyCollection<ApiModel.OrchestrationInstance.OrchestrationInstanceDto> MapToDto(
        this IReadOnlyCollection<DomainModel.OrchestrationInstance> entities)
    {
        return entities
            .Select(instance => instance.MapToDto())
            .ToList();
    }
}
#pragma warning restore SA1118 // Parameter should not span multiple lines
