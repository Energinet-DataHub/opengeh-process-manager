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
    public static readonly ActorRole MeteringPointAdministrator = new("MeteringPointAdministrator");
    public static readonly ActorRole EnergySupplier = new("EnergySupplier");
    public static readonly ActorRole GridAccessProvider = new("GridAccessProvider");
    public static readonly ActorRole MeteredDataAdministrator = new("MeteredDataAdministrator");
    public static readonly ActorRole MeteredDataResponsible = new("MeteredDataResponsible");
    public static readonly ActorRole BalanceResponsibleParty = new("BalanceResponsibleParty");
    public static readonly ActorRole ImbalanceSettlementResponsible = new("ImbalanceSettlementResponsible");
    public static readonly ActorRole SystemOperator = new("SystemOperator");
    public static readonly ActorRole DanishEnergyAgency = new("DanishEnergyAgency");
    public static readonly ActorRole Delegated = new("Delegated");
    public static readonly ActorRole DataHubAdministrator = new("DataHubAdministrator");

    [JsonConstructor]
    private ActorRole(string name)
        : base(name)
    {
    }

    public static ActorRole From(Contracts.ActorRoleV1 actorRoleV1)
    {
        return FromName(actorRoleV1.ToString());
    }

    public Contracts.ActorRoleV1 ToActorRoleV1()
    {
        if (!Enum.TryParse<Contracts.ActorRoleV1>(Name, out var actorRoleV1))
            throw new ArgumentOutOfRangeException(nameof(Name), Name, "Cannot convert to ActorRoleV1");

        return actorRoleV1;
    }
}
