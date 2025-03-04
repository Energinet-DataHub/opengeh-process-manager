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
    public static DomainModel.OrchestrationInstance.UserIdentity MapToDomain(this ApiModel.OrchestrationInstance.UserIdentityDto dto)
    {
        return new DomainModel.OrchestrationInstance.UserIdentity(
            UserId: new DomainModel.OrchestrationInstance.UserId(dto.UserId),
            new DomainModel.OrchestrationInstance.Actor(
                Number: dto.ActorNumber,
                Role: dto.ActorRole));
    }

    public static DomainModel.OrchestrationInstance.ActorIdentity MapToDomain(this ApiModel.OrchestrationInstance.ActorIdentityDto dto)
    {
        return new DomainModel.OrchestrationInstance.ActorIdentity(
            new DomainModel.OrchestrationInstance.Actor(
                Number: dto.ActorNumber,
                Role: dto.ActorRole));
    }

    public static DomainModel.OrchestrationDescription.OrchestrationDescriptionUniqueName MapToDomain(this ApiModel.OrchestrationDescription.OrchestrationDescriptionUniqueNameDto dto)
    {
        return new DomainModel.OrchestrationDescription.OrchestrationDescriptionUniqueName(dto.Name, dto.Version);
    }
}
