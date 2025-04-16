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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using ElectricityMarketModels = Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;

public static class ElectricityMarketMasterDataMapper
{
    public static readonly ValueObjectsMap<ElectricityMarketModels.MeteringPointType, MeteringPointType> MeteringPointTypeMap = new()
    {
        { ElectricityMarketModels.MeteringPointType.Consumption, MeteringPointType.Consumption },
        { ElectricityMarketModels.MeteringPointType.Production, MeteringPointType.Production },
        { ElectricityMarketModels.MeteringPointType.Exchange, MeteringPointType.Exchange },
        { ElectricityMarketModels.MeteringPointType.VEProduction, MeteringPointType.VeProduction },
        { ElectricityMarketModels.MeteringPointType.Analysis, MeteringPointType.Analysis },
        { ElectricityMarketModels.MeteringPointType.NotUsed, MeteringPointType.NotUsed },
        { ElectricityMarketModels.MeteringPointType.SurplusProductionGroup6, MeteringPointType.SurplusProductionGroup6 },
        { ElectricityMarketModels.MeteringPointType.NetProduction, MeteringPointType.NetProduction },
        { ElectricityMarketModels.MeteringPointType.SupplyToGrid, MeteringPointType.SupplyToGrid },
        { ElectricityMarketModels.MeteringPointType.ConsumptionFromGrid, MeteringPointType.ConsumptionFromGrid },
        { ElectricityMarketModels.MeteringPointType.WholesaleServicesOrInformation, MeteringPointType.WholesaleServicesInformation },
        { ElectricityMarketModels.MeteringPointType.OwnProduction, MeteringPointType.OwnProduction },
        { ElectricityMarketModels.MeteringPointType.NetFromGrid, MeteringPointType.NetFromGrid },
        { ElectricityMarketModels.MeteringPointType.NetToGrid, MeteringPointType.NetToGrid },
        { ElectricityMarketModels.MeteringPointType.TotalConsumption, MeteringPointType.TotalConsumption },
        { ElectricityMarketModels.MeteringPointType.NetLossCorrection, MeteringPointType.NetLossCorrection },
        { ElectricityMarketModels.MeteringPointType.ElectricalHeating, MeteringPointType.ElectricalHeating },
        { ElectricityMarketModels.MeteringPointType.NetConsumption, MeteringPointType.NetConsumption },
        { ElectricityMarketModels.MeteringPointType.OtherConsumption, MeteringPointType.OtherConsumption },
        { ElectricityMarketModels.MeteringPointType.OtherProduction, MeteringPointType.OtherProduction },
        { ElectricityMarketModels.MeteringPointType.CapacitySettlement, MeteringPointType.CapacitySettlement },
        { ElectricityMarketModels.MeteringPointType.ExchangeReactiveEnergy, MeteringPointType.ExchangeReactiveEnergy },
        { ElectricityMarketModels.MeteringPointType.CollectiveNetProduction, MeteringPointType.CollectiveNetProduction },
        { ElectricityMarketModels.MeteringPointType.CollectiveNetConsumption, MeteringPointType.CollectiveNetConsumption },
        { ElectricityMarketModels.MeteringPointType.ActivatedDownregulation, MeteringPointType.ActivatedDownRegulation },
        { ElectricityMarketModels.MeteringPointType.ActivatedUpregulation, MeteringPointType.ActivatedUpRegulation },
        { ElectricityMarketModels.MeteringPointType.ActualConsumption, MeteringPointType.ActualConsumption },
        { ElectricityMarketModels.MeteringPointType.ActualProduction, MeteringPointType.ActualProduction },
        { ElectricityMarketModels.MeteringPointType.InternalUse, MeteringPointType.InternalUse },
    };

    public static readonly ValueObjectsMap<ElectricityMarketModels.MeasureUnit, MeasurementUnit> MeasureUnitMap = new()
    {
        { ElectricityMarketModels.MeasureUnit.Ampere, MeasurementUnit.Ampere },
        { ElectricityMarketModels.MeasureUnit.STK, MeasurementUnit.Pieces },
        { ElectricityMarketModels.MeasureUnit.kVArh, MeasurementUnit.KiloVoltAmpereReactiveHour },
        { ElectricityMarketModels.MeasureUnit.kWh, MeasurementUnit.KilowattHour },
        { ElectricityMarketModels.MeasureUnit.kW, MeasurementUnit.Kilowatt },
        { ElectricityMarketModels.MeasureUnit.MW, MeasurementUnit.Megawatt },
        { ElectricityMarketModels.MeasureUnit.MWh, MeasurementUnit.MegawattHour },
        { ElectricityMarketModels.MeasureUnit.Tonne, MeasurementUnit.MetricTon },
        { ElectricityMarketModels.MeasureUnit.MVAr, MeasurementUnit.MegaVoltAmpereReactivePower },
        { ElectricityMarketModels.MeasureUnit.DanishTariffCode, MeasurementUnit.DanishTariffCode },
    };

    public static readonly ValueObjectsMap<ElectricityMarketModels.MeteringPointSubType, MeteringPointSubType> MeteringPointSubTypeMap = new()
    {
        { ElectricityMarketModels.MeteringPointSubType.Physical, MeteringPointSubType.Physical },
        { ElectricityMarketModels.MeteringPointSubType.Virtual, MeteringPointSubType.Virtual },
        { ElectricityMarketModels.MeteringPointSubType.Calculated, MeteringPointSubType.Calculated },
    };

    public static readonly ValueObjectsMap<ElectricityMarketModels.ConnectionState, ConnectionState> ConnectionStateMap = new()
    {
        { ElectricityMarketModels.ConnectionState.NotUsed, ConnectionState.NotUsed },
        { ElectricityMarketModels.ConnectionState.ClosedDown, ConnectionState.ClosedDown },
        { ElectricityMarketModels.ConnectionState.New, ConnectionState.New },
        { ElectricityMarketModels.ConnectionState.Connected, ConnectionState.Connected },
        { ElectricityMarketModels.ConnectionState.Disconnected, ConnectionState.Disconnected },
    };

    public static readonly ValueObjectsMap<string, Resolution> ResolutionMap = new()
    {
        { "PT15M", Resolution.QuarterHourly },
        { "PT1H", Resolution.Hourly },
        { "P1M", Resolution.Monthly },
        { "ANDET", Resolution.Other },
    };
}
