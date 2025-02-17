﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

/// <summary>
/// A user identity performing a Process Manager operation.
/// </summary>
public record UserIdentity(UserId UserId, Actor Actor)
    : OperatingIdentity
{
    public override IOperatingIdentityDto ToDto()
    {
        return new UserIdentityDto(UserId.Value, Actor.Number, Actor.Role);
    }

    public static UserIdentity FromDto(UserIdentityDto dto)
    {
        return new UserIdentity(
            new UserId(dto.UserId),
            new Actor(dto.ActorNumber, dto.ActorRole));
    }
}
