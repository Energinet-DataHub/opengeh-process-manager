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

using System.Text.Json.Serialization;

namespace Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;

public record ActorRole : DataHubRecordType<ActorRole>
{
    public static readonly ActorRole MeteringPointAdministrator = new("MeteringPointAdministrator", 1);
    public static readonly ActorRole EnergySupplier = new("EnergySupplier", 2);
    public static readonly ActorRole GridAccessProvider = new("GridAccessProvider", 3);
    public static readonly ActorRole MeteredDataAdministrator = new("MeteredDataAdministrator", 4);
    public static readonly ActorRole MeteredDataResponsible = new("MeteredDataResponsible", 5);
    public static readonly ActorRole BalanceResponsibleParty = new("BalanceResponsibleParty", 6);
    public static readonly ActorRole ImbalanceSettlementResponsible = new("ImbalanceSettlementResponsible", 7);
    public static readonly ActorRole SystemOperator = new("SystemOperator", 8);
    public static readonly ActorRole DanishEnergyAgency = new("DanishEnergyAgency", 9);
    public static readonly ActorRole Delegated = new("Delegated", 10);
    public static readonly ActorRole DataHubAdministrator = new("DataHubAdministrator", 11);

    [JsonConstructor]
    private ActorRole(string name, byte byteValue)
        : base(name)
    {
        ByteValue = byteValue;
    }

    /// <summary>
    /// Each actor role is assigned a unique byte value, allowing for efficient storage and retrieval.
    /// It ensures consistency and performance while mapping roles to database values and application logic.
    /// Creating a new actor role must be done with caution to avoid conflicts with existing roles.
    /// Changing the byte value of an existing role is not allowed, as it would break the existing data.
    /// </summary>
    public byte ByteValue { get; }

    public static ActorRole From(Contracts.ActorRoleV1 actorRoleV1)
    {
        return FromName(actorRoleV1.ToString());
    }

    public static ActorRole FromByteValue(byte byteValue)
    {
        return GetAll<ActorRole>().FirstOrDefault(t => t.ByteValue == byteValue)
               ?? throw new InvalidOperationException(
                   $"Byte value \"{byteValue}\" is not a valid {nameof(ActorRole)}");
    }

    public Contracts.ActorRoleV1 ToActorRoleV1()
    {
        if (!Enum.TryParse<Contracts.ActorRoleV1>(Name, out var actorRoleV1))
            throw new ArgumentOutOfRangeException(nameof(Name), Name, $"Cannot convert {nameof(ActorRole)} to {nameof(Contracts.ActorRoleV1)}");

        return actorRoleV1;
    }
}
