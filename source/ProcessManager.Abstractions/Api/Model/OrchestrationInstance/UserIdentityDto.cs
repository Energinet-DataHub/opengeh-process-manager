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

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

/// <summary>
/// The intention of this record is to carry information about the user/system that
/// initiated the operation.
/// The information must be extracted by DataHub 3 "boundary subsystems", and used
/// by Process Manager API's to further filter data or validate requests.
/// </summary>
/// <param name="UserId"></param>
/// <param name="ActorNumber"></param>
/// <param name="ActorRole"></param>
/// <param name="UserPermissions">Contains what would be similar to "roles" in access tokens.</param>
public record UserIdentityDto(
    Guid UserId,
    ActorNumber ActorNumber,
    ActorRole ActorRole,
    IReadOnlyCollection<string> UserPermissions)
        : IOperatingIdentityDto;
