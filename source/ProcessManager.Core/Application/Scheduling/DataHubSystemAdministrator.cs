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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Core.Application.Scheduling;

/// <summary>
/// The DataHub administrator user that the Market Participant is seeded with.
/// </summary>
public static class DataHubSystemAdministrator
{
    private static readonly UserId _userId = new UserId(Guid.Parse("C861C5E2-8DDA-43E5-A5D0-B94834EE3FF6"));

    private static readonly Actor _actor = new Actor(
        ActorNumber.Create("5790001330583"),
        ActorRole.DataHubAdministrator);

    /// <summary>
    /// We combine a "known user id" (known by Market Participant)
    /// and the DataHub Administrator actor number/role to create an
    /// operating identity for recurring jobs.
    /// </summary>
    public static UserIdentity UserIdentity => new UserIdentity(_userId, _actor);
}
