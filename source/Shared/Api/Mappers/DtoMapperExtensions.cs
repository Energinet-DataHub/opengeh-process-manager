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
using DomainModel = Energinet.DataHub.ProcessManager.Core.Domain;

namespace Energinet.DataHub.ProcessManager.Shared.Api.Mappers;

/// <summary>
/// Extensions to map most used DTO's to their domain counterpart.
/// </summary>
internal static class DtoMapperExtensions
{
    public static DomainModel.OrchestrationInstance.UserIdentity MapToDomain(
        this ApiModel.OrchestrationInstance.UserIdentityDto dto)
    {
        return new DomainModel.OrchestrationInstance.UserIdentity(
            UserId: new DomainModel.OrchestrationInstance.UserId(dto.UserId),
            new DomainModel.OrchestrationInstance.Actor(
                Number: dto.ActorNumber,
                Role: dto.ActorRole));
    }

    public static DomainModel.OrchestrationInstance.ActorIdentity MapToDomain(
        this ApiModel.OrchestrationInstance.ActorIdentityDto dto)
    {
        return new DomainModel.OrchestrationInstance.ActorIdentity(
            new DomainModel.OrchestrationInstance.Actor(
                Number: dto.ActorNumber,
                Role: dto.ActorRole));
    }

    public static DomainModel.OrchestrationDescription.OrchestrationDescriptionUniqueName MapToDomain(
        this ApiModel.OrchestrationDescription.OrchestrationDescriptionUniqueNameDto dto)
    {
        return new DomainModel.OrchestrationDescription.OrchestrationDescriptionUniqueName(dto.Name, dto.Version);
    }

    #region OrchestrationInstanceLifecycleState

    public static IReadOnlyCollection<DomainModel.OrchestrationInstance.OrchestrationInstanceLifecycleState>? MapToDomain(
        this IReadOnlyCollection<ApiModel.OrchestrationInstance.OrchestrationInstanceLifecycleState>? dtoStates)
    {
        return dtoStates?
            .Select(state => state.MapToDomain())
            .ToList();
    }

    public static DomainModel.OrchestrationInstance.OrchestrationInstanceLifecycleState? MapToDomain(
        this ApiModel.OrchestrationInstance.OrchestrationInstanceLifecycleState? dtoEnum)
    {
        return dtoEnum.HasValue
            ? MapToDomain(dtoEnum.Value)
            : null;
    }

    public static DomainModel.OrchestrationInstance.OrchestrationInstanceLifecycleState MapToDomain(
        this ApiModel.OrchestrationInstance.OrchestrationInstanceLifecycleState dtoEnum)
    {
        return Enum
            .TryParse<DomainModel.OrchestrationInstance.OrchestrationInstanceLifecycleState>(
                dtoEnum.ToString(),
                ignoreCase: true,
                out var result)
            ? result
            : throw new InvalidOperationException($"Invalid State '{dtoEnum}'; cannot be mapped.");
    }

    #endregion

    #region OrchestrationInstanceTerminationState

    public static DomainModel.OrchestrationInstance.OrchestrationInstanceTerminationState? MapToDomain(
        this ApiModel.OrchestrationInstance.OrchestrationInstanceTerminationState? dtoEnum)
    {
        return dtoEnum.HasValue
            ? MapToDomain(dtoEnum.Value)
            : null;
    }

    public static DomainModel.OrchestrationInstance.OrchestrationInstanceTerminationState MapToDomain(
        this ApiModel.OrchestrationInstance.OrchestrationInstanceTerminationState dtoEnum)
    {
        return Enum
            .TryParse<DomainModel.OrchestrationInstance.OrchestrationInstanceTerminationState>(
                dtoEnum.ToString(),
                ignoreCase: true,
                out var result)
            ? result
            : throw new InvalidOperationException($"Invalid State '{dtoEnum}'; cannot be mapped.");
    }

    #endregion
}
