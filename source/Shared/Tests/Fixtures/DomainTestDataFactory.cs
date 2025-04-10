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

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

/// <summary>
/// Create types in the ProcessManager.Core.Domain namespace.
/// </summary>
public static class DomainTestDataFactory
{
    public static readonly IdentityTuple EnergySupplier;

    public static readonly IdentityTuple BalanceResponsibleParty;

    static DomainTestDataFactory()
    {
        EnergySupplier = new(
            "1234567890222",
            ActorRole.EnergySupplier,
            "6FF394EF-4B0D-4EBE-8477-522544C34E84");

        BalanceResponsibleParty = new(
            "1234567890333",
            ActorRole.BalanceResponsibleParty,
            "0CBB5427-E406-4E6F-A975-E21A69A3CA2A");
    }

    public sealed record IdentityTuple
    {
        public IdentityTuple(
            string actorNumber,
            ActorRole actorRole,
            string userId)
        {
            ActorIdentity = new(
                new Actor(
                    ActorNumber.Create(actorNumber),
                    actorRole));

            UserIdentity = new(
                new UserId(Guid.Parse(userId)),
                ActorIdentity.Actor);
        }

        public UserIdentity UserIdentity { get; }

        public ActorIdentity ActorIdentity { get; }
    }
}
