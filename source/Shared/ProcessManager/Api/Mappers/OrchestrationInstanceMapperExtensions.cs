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
using ApiModel = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using DomainModel = Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Api.Mappers;

#pragma warning disable SA1118 // Parameter should not span multiple lines
internal static class OrchestrationInstanceMapperExtensions
{
    public static OrchestrationInstanceDto MapToDto(
        this DomainModel.OrchestrationInstance entity)
    {
        return new ApiModel.OrchestrationInstanceDto(
            Id: entity.Id.Value,
            Lifecycle: entity.Lifecycle.MapToDto(),
            ParameterValue: entity.ParameterValue.AsExpandoObject(),
            Steps: entity.Steps.Select(step => step.MapToDto()).ToList(),
            CustomState: entity.CustomState.Value);
    }

    public static OrchestrationInstanceLifecycleStateDto MapToDto(
        this DomainModel.OrchestrationInstanceLifecycleState entity)
    {
        return new ApiModel.OrchestrationInstanceLifecycleStateDto(
            CreatedBy: entity.CreatedBy.Value.MapToDto(),
            State: Enum
                .TryParse<OrchestrationInstanceLifecycleStates>(
                    entity.State.ToString(),
                    ignoreCase: true,
                    out var lifecycleStateResult)
                ? lifecycleStateResult
                : throw new InvalidOperationException($"Invalid State '{entity.State}'; cannot be mapped."),
            TerminationState: Enum
                .TryParse<OrchestrationInstanceTerminationStates>(
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

    public static IOperatingIdentityDto MapToDto(
        this DomainModel.OperatingIdentity entity)
    {
        switch (entity)
        {
            case DomainModel.ActorIdentity actor:
                return new ApiModel.ActorIdentityDto(
                    ActorId: actor.ActorId.Value);

            case DomainModel.UserIdentity user:
                return new ApiModel.UserIdentityDto(
                    UserId: user.UserId.Value,
                    ActorId: user.ActorId.Value);

            default:
                throw new InvalidOperationException($"Invalid type '{entity.GetType()}'; cannot be mapped.");
        }
    }

    public static StepInstanceDto MapToDto(
        this DomainModel.StepInstance entity)
    {
        return new ApiModel.StepInstanceDto(
            Id: entity.Id.Value,
            Lifecycle: entity.Lifecycle.MapToDto(),
            Description: entity.Description,
            Sequence: entity.Sequence,
            CustomState: entity.CustomState.Value);
    }

    public static StepInstanceLifecycleStateDto MapToDto(
        this DomainModel.StepInstanceLifecycleState entity)
    {
        return new ApiModel.StepInstanceLifecycleStateDto(
            State: Enum
                .TryParse<StepInstanceLifecycleStates>(
                    entity.State.ToString(),
                    ignoreCase: true,
                    out var lifecycleStateResult)
                ? lifecycleStateResult
                : throw new InvalidOperationException($"Invalid State '{entity.State}'; cannot be mapped."),
            TerminationState: Enum
                .TryParse<OrchestrationStepTerminationStates>(
                    entity.TerminationState.ToString(),
                    ignoreCase: true,
                    out var terminationStateResult)
                ? terminationStateResult
                : null,
            StartedAt: entity.StartedAt?.ToDateTimeOffset(),
            TerminatedAt: entity.TerminatedAt?.ToDateTimeOffset());
    }

    public static IReadOnlyCollection<OrchestrationInstanceDto> MapToDto(
        this IReadOnlyCollection<DomainModel.OrchestrationInstance> entities)
    {
        return entities
            .Select(instance => instance.MapToDto())
            .ToList();
    }
}
#pragma warning restore SA1118 // Parameter should not span multiple lines
