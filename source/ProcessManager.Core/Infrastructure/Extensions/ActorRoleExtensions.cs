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

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions;

public static class ActorRoleExtensions
{
    /// <summary>
    /// Actor role to byte value mapping. The byte values are saved to the database, so they MUST NOT be changed.
    /// </summary>
    internal static readonly IReadOnlyDictionary<ActorRole, byte> ActorRoleToByteValueMap = new Dictionary<ActorRole, byte>
    {
        { ActorRole.MeteringPointAdministrator, 1 },
        { ActorRole.EnergySupplier, 2 },
        { ActorRole.GridAccessProvider, 3 },
        { ActorRole.MeteredDataAdministrator, 4 },
        { ActorRole.MeteredDataResponsible, 5 },
        { ActorRole.BalanceResponsibleParty, 6 },
        { ActorRole.ImbalanceSettlementResponsible, 7 },
        { ActorRole.SystemOperator, 8 },
        { ActorRole.DanishEnergyAgency, 9 },
        { ActorRole.Delegated, 10 },
        { ActorRole.DataHubAdministrator, 11 },
    };

    private static readonly IReadOnlyDictionary<byte, ActorRole> _byteValueToActorRoleMap = ActorRoleToByteValueMap
        .ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    public static byte ToByteValue(this ActorRole actorRole) => ActorRoleToByteValueMap[actorRole];

    public static ActorRole ToActorRole(this byte actorRoleByteValue) => _byteValueToActorRoleMap[actorRoleByteValue];
}
