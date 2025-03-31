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
            entity.CustomState.SerializedValue,
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
            CustomState: entity.CustomState.SerializedValue,
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
            State: entity.State.MapToDto(),
            TerminationState: entity.TerminationState.MapToDto(),
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
        switch (entity)
        {
            case DomainModel.ActorIdentity actorIdentity:
                return new ApiModel.OrchestrationInstance.ActorIdentityDto(
                    ActorNumber: actorIdentity.Actor.Number,
                    ActorRole: actorIdentity.Actor.Role);

            case DomainModel.UserIdentity userIdentity:
                return new ApiModel.OrchestrationInstance.UserIdentityDto(
                    UserId: userIdentity.UserId.Value,
                    ActorNumber: userIdentity.Actor.Number,
                    ActorRole: userIdentity.Actor.Role);

            default:
                throw new InvalidOperationException($"Invalid type '{entity.GetType()}'; cannot be mapped.");
        }
    }

    public static ApiModel.OrchestrationInstance.StepInstanceDto MapToDto(
        this DomainModel.StepInstance entity)
    {
        return new ApiModel.OrchestrationInstance.StepInstanceDto(
            Id: entity.Id.Value,
            Lifecycle: entity.Lifecycle.MapToDto(),
            Description: entity.Description,
            Sequence: entity.Sequence,
            CustomState: entity.CustomState.SerializedValue);
    }

    public static ApiModel.OrchestrationInstance.StepInstanceLifecycleDto MapToDto(
        this DomainModel.StepInstanceLifecycle entity)
    {
        return new ApiModel.OrchestrationInstance.StepInstanceLifecycleDto(
            State: entity.State.MapToDto(),
            TerminationState: entity.TerminationState.MapToDto(),
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

    private static ApiModel.OrchestrationInstance.OrchestrationInstanceLifecycleState MapToDto(
    this DomainModel.OrchestrationInstanceLifecycleState domainEnum)
    {
        return Enum
            .TryParse<ApiModel.OrchestrationInstance.OrchestrationInstanceLifecycleState>(
                domainEnum.ToString(),
                ignoreCase: true,
                out var result)
            ? result
            : throw new InvalidOperationException($"Invalid State '{domainEnum}'; cannot be mapped.");
    }

    private static ApiModel.OrchestrationInstance.OrchestrationInstanceTerminationState? MapToDto(
        this DomainModel.OrchestrationInstanceTerminationState? domainEnum)
    {
        if (!domainEnum.HasValue)
            return null;

        return Enum
            .TryParse<ApiModel.OrchestrationInstance.OrchestrationInstanceTerminationState>(
                domainEnum.ToString(),
                ignoreCase: true,
                out var result)
            ? result
            : throw new InvalidOperationException($"Invalid State '{domainEnum}'; cannot be mapped.");
    }

    private static ApiModel.OrchestrationInstance.StepInstanceLifecycleState MapToDto(
        this DomainModel.StepInstanceLifecycleState domainEnum)
    {
        return Enum
            .TryParse<ApiModel.OrchestrationInstance.StepInstanceLifecycleState>(
                domainEnum.ToString(),
                ignoreCase: true,
                out var result)
            ? result
            : throw new InvalidOperationException($"Invalid State '{domainEnum}'; cannot be mapped.");
    }

    private static ApiModel.OrchestrationInstance.OrchestrationStepTerminationState? MapToDto(
        this DomainModel.StepInstanceTerminationState? domainEnum)
    {
        if (!domainEnum.HasValue)
            return null;

        return Enum
            .TryParse<ApiModel.OrchestrationInstance.StepInstanceTerminationState>(
                domainEnum.ToString(),
                ignoreCase: true,
                out var result)
            ? result
            : throw new InvalidOperationException($"Invalid State '{domainEnum}'; cannot be mapped.");
    }
}
#pragma warning restore SA1118 // Parameter should not span multiple lines
