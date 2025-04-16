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

using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Mappers;
using PMTypes = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Mapper;

public static class MeteringPointMasterDataMapper
{
    public static readonly ValueObjectsMap<MeteringPointType, PMTypes.MeteringPointType> MeteringPointTypeMap = new()
    {
        { MeteringPointType.Consumption, PMTypes.MeteringPointType.Consumption },
        { MeteringPointType.Production, PMTypes.MeteringPointType.Production },
        { MeteringPointType.Exchange, PMTypes.MeteringPointType.Exchange },
        { MeteringPointType.VEProduction, PMTypes.MeteringPointType.VeProduction },
        { MeteringPointType.Analysis, PMTypes.MeteringPointType.Analysis },
        { MeteringPointType.NotUsed, PMTypes.MeteringPointType.NotUsed },
        { MeteringPointType.SurplusProductionGroup6, PMTypes.MeteringPointType.SurplusProductionGroup6 },
        { MeteringPointType.NetProduction, PMTypes.MeteringPointType.NetProduction },
        { MeteringPointType.SupplyToGrid, PMTypes.MeteringPointType.SupplyToGrid },
        { MeteringPointType.ConsumptionFromGrid, PMTypes.MeteringPointType.ConsumptionFromGrid },
        { MeteringPointType.WholesaleServicesOrInformation, PMTypes.MeteringPointType.WholesaleServicesInformation },
        { MeteringPointType.OwnProduction, PMTypes.MeteringPointType.OwnProduction },
        { MeteringPointType.NetFromGrid, PMTypes.MeteringPointType.NetFromGrid },
        { MeteringPointType.NetToGrid, PMTypes.MeteringPointType.NetToGrid },
        { MeteringPointType.TotalConsumption, PMTypes.MeteringPointType.TotalConsumption },
        { MeteringPointType.NetLossCorrection, PMTypes.MeteringPointType.NetLossCorrection },
        { MeteringPointType.ElectricalHeating, PMTypes.MeteringPointType.ElectricalHeating },
        { MeteringPointType.NetConsumption, PMTypes.MeteringPointType.NetConsumption },
        { MeteringPointType.OtherConsumption, PMTypes.MeteringPointType.OtherConsumption },
        { MeteringPointType.OtherProduction, PMTypes.MeteringPointType.OtherProduction },
        { MeteringPointType.CapacitySettlement, PMTypes.MeteringPointType.CapacitySettlement },
        { MeteringPointType.ExchangeReactiveEnergy, PMTypes.MeteringPointType.ExchangeReactiveEnergy },
        { MeteringPointType.CollectiveNetProduction, PMTypes.MeteringPointType.CollectiveNetProduction },
        { MeteringPointType.CollectiveNetConsumption, PMTypes.MeteringPointType.CollectiveNetConsumption },
        { MeteringPointType.InternalUse, PMTypes.MeteringPointType.InternalUse },
    };

    public static readonly ValueObjectsMap<MeasureUnit, PMTypes.MeasurementUnit> MeasureUnitMap = new()
    {
        { MeasureUnit.Ampere, PMTypes.MeasurementUnit.Ampere },
        { MeasureUnit.STK, PMTypes.MeasurementUnit.Pieces },
        { MeasureUnit.kVArh, PMTypes.MeasurementUnit.KiloVoltAmpereReactiveHour },
        { MeasureUnit.kWh, PMTypes.MeasurementUnit.KilowattHour },
        { MeasureUnit.kW, PMTypes.MeasurementUnit.Kilowatt },
        { MeasureUnit.MW, PMTypes.MeasurementUnit.Megawatt },
        { MeasureUnit.MWh, PMTypes.MeasurementUnit.MegawattHour },
        { MeasureUnit.Tonne, PMTypes.MeasurementUnit.MetricTon },
        { MeasureUnit.MVAr, PMTypes.MeasurementUnit.MegaVoltAmpereReactivePower },
        { MeasureUnit.DanishTariffCode, PMTypes.MeasurementUnit.DanishTariffCode },
    };

    public static readonly ValueObjectsMap<MeteringPointSubType, Model.MeteringPointSubType> MeteringPointSubTypeMap = new()
    {
        { MeteringPointSubType.Physical, Model.MeteringPointSubType.Physical },
        { MeteringPointSubType.Virtual, Model.MeteringPointSubType.Virtual },
        { MeteringPointSubType.Calculated, Model.MeteringPointSubType.Calculated },
    };

    public static readonly ValueObjectsMap<ConnectionState, Model.ConnectionState> ConnectionStateMap = new()
    {
        { ConnectionState.NotUsed, Model.ConnectionState.NotUsed },
        { ConnectionState.ClosedDown, Model.ConnectionState.ClosedDown },
        { ConnectionState.New, Model.ConnectionState.New },
        { ConnectionState.Connected, Model.ConnectionState.Connected },
        { ConnectionState.Disconnected, Model.ConnectionState.Disconnected },
    };

    public static readonly ValueObjectsMap<string, PMTypes.Resolution> ResolutionMap = new()
    {
        { "PT15M", PMTypes.Resolution.QuarterHourly },
        { "PT1H", PMTypes.Resolution.Hourly },
        { "P1M", PMTypes.Resolution.Monthly },
        { "ANDET", PMTypes.Resolution.Other },
    };
}
