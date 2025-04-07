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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

public record MeteringPointType : DataHubRecordType<MeteringPointType>
{
    public static readonly MeteringPointType Consumption = new("Consumption");
    public static readonly MeteringPointType Production = new("Production");
    public static readonly MeteringPointType Exchange = new("Exchange");

    // Child metering point types
    public static readonly MeteringPointType VeProduction = new("VeProduction");
    public static readonly MeteringPointType NetProduction = new("NetProduction");
    public static readonly MeteringPointType SupplyToGrid = new("SupplyToGrid");
    public static readonly MeteringPointType ConsumptionFromGrid = new("ConsumptionFromGrid");
    public static readonly MeteringPointType WholesaleServicesInformation = new("WholesaleServicesInformation");
    public static readonly MeteringPointType OwnProduction = new("OwnProduction");
    public static readonly MeteringPointType NetFromGrid = new("NetFromGrid");
    public static readonly MeteringPointType NetToGrid = new("NetToGrid");
    public static readonly MeteringPointType TotalConsumption = new("TotalConsumption");
    public static readonly MeteringPointType ElectricalHeating = new("ElectricalHeating");
    public static readonly MeteringPointType NetConsumption = new("NetConsumption");
    public static readonly MeteringPointType CapacitySettlement = new("CapacitySettlement");
    public static readonly MeteringPointType Analysis = new("Analysis");
    public static readonly MeteringPointType NotUsed = new("NotUsed");
    public static readonly MeteringPointType SurplusProductionGroup6 = new("SurplusProductionGroup6");
    public static readonly MeteringPointType NetLossCorrection = new("NetLossCorrection");
    public static readonly MeteringPointType OtherConsumption = new("OtherConsumption");
    public static readonly MeteringPointType OtherProduction = new("OtherProduction");
    public static readonly MeteringPointType ExchangeReactiveEnergy = new("ExchangeReactiveEnergy");
    public static readonly MeteringPointType CollectiveNetProduction = new("CollectiveNetProduction");
    public static readonly MeteringPointType CollectiveNetConsumption = new("CollectiveNetConsumption");
    public static readonly MeteringPointType ActivatedDownRegulation = new("ActivatedDownRegulation");
    public static readonly MeteringPointType ActivatedUpRegulation = new("ActivatedUpRegulation");
    public static readonly MeteringPointType ActualConsumption = new("ActualConsumption");
    public static readonly MeteringPointType ActualProduction = new("ActualProduction");
    public static readonly MeteringPointType InternalUse = new("InternalUse");

    [JsonConstructor]
    private MeteringPointType(string name)
        : base(name)
    {
    }
}
