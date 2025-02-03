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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;

[Serializable]
public class ActorRole : DataHubType<ActorRole>
{
    public static readonly ActorRole MeteringPointAdministrator = new("MeteringPointAdministrator", "DDZ");
    public static readonly ActorRole EnergySupplier = new("EnergySupplier", "DDQ");
    public static readonly ActorRole GridAccessProvider = new("GridAccessProvider", "DDM");
    public static readonly ActorRole MeteredDataAdministrator = new("MeteredDataAdministrator", "DGL");
    public static readonly ActorRole MeteredDataResponsible = new("MeteredDataResponsible", "MDR");
    public static readonly ActorRole BalanceResponsibleParty = new("BalanceResponsibleParty", "DDK");
    public static readonly ActorRole ImbalanceSettlementResponsible = new("ImbalanceSettlementResponsible", "DDX");
    public static readonly ActorRole SystemOperator = new("SystemOperator", "EZ");
    public static readonly ActorRole DanishEnergyAgency = new("DanishEnergyAgency", "STS");
    public static readonly ActorRole Delegated = new("Delegated", "DEL");
    public static readonly ActorRole DataHubAdministrator = new("DataHubAdministrator", string.Empty);

    [JsonConstructor]
    private ActorRole(string name, string code)
        : base(name, code)
    {
    }
}
