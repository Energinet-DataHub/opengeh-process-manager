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
using PMTypes = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Mapper;

public static class MeteringPointMasterDataMapper
{
    public static readonly Dictionary<MeteringPointType, PMTypes.MeteringPointType> MeteringPointTypeMap = new()
    {
        { MeteringPointType.Consumption, PMTypes.MeteringPointType.Consumption },
        { MeteringPointType.Production, PMTypes.MeteringPointType.Production },
        { MeteringPointType.Exchange, PMTypes.MeteringPointType.Exchange },
    };

    public static readonly Dictionary<MeasureUnit, PMTypes.MeasurementUnit> MeasureUnitMap = new()
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

    public static readonly Dictionary<MeteringPointSubType, Model.MeteringPointSubType> MeteringPointSubTypeMap = new()
    {
        { MeteringPointSubType.Physical, Model.MeteringPointSubType.Physical },
        { MeteringPointSubType.Virtual, Model.MeteringPointSubType.Virtual },
        { MeteringPointSubType.Calculated, Model.MeteringPointSubType.Calculated },
    };

    public static readonly Dictionary<ConnectionState, Model.ConnectionState> ConnectionStateMap = new()
    {
        { ConnectionState.NotUsed, Model.ConnectionState.NotUsed },
        { ConnectionState.ClosedDown, Model.ConnectionState.ClosedDown },
        { ConnectionState.New, Model.ConnectionState.New },
        { ConnectionState.Connected, Model.ConnectionState.Connected },
        { ConnectionState.Disconnected, Model.ConnectionState.Disconnected },
    };

    public static readonly Dictionary<string, PMTypes.Resolution> ResolutionMap = new()
    {
        { "PT15M", PMTypes.Resolution.QuarterHourly },
        { "PT1H", PMTypes.Resolution.Hourly },
    };
}
