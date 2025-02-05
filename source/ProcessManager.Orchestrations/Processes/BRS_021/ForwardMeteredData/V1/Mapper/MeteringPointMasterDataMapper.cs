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

using EMTypes = Energinet.DataHub.ElectricityMarket.Integration;
using PMTypes = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Mapper;

public static class MeteringPointMasterDataMapper
{
    public static readonly Dictionary<EMTypes.MeteringPointType, PMTypes.MeteringPointType> MeteringPointTypeMap = new()
    {
        { EMTypes.MeteringPointType.Consumption, PMTypes.MeteringPointType.Consumption },
        { EMTypes.MeteringPointType.Production, PMTypes.MeteringPointType.Production },
        { EMTypes.MeteringPointType.Exchange, PMTypes.MeteringPointType.Exchange },
    };

    public static readonly Dictionary<EMTypes.MeasureUnit, PMTypes.MeasurementUnit> MeasureUnitMap = new()
    {
        { EMTypes.MeasureUnit.Ampere, PMTypes.MeasurementUnit.Ampere },
        { EMTypes.MeasureUnit.STK, PMTypes.MeasurementUnit.Pieces },
        { EMTypes.MeasureUnit.kVArh, PMTypes.MeasurementUnit.KiloVoltAmpereReactiveHour },
        { EMTypes.MeasureUnit.kWh, PMTypes.MeasurementUnit.KilowattHour },
        { EMTypes.MeasureUnit.kW, PMTypes.MeasurementUnit.Kilowatt },
        { EMTypes.MeasureUnit.MW, PMTypes.MeasurementUnit.Megawatt },
        { EMTypes.MeasureUnit.MWh, PMTypes.MeasurementUnit.MegawattHour },
        { EMTypes.MeasureUnit.Tonne, PMTypes.MeasurementUnit.MetricTon },
        { EMTypes.MeasureUnit.MVAr, PMTypes.MeasurementUnit.MegaVoltAmpereReactivePower },
        { EMTypes.MeasureUnit.DanishTariffCode, PMTypes.MeasurementUnit.DanishTariffCode },
    };

    public static readonly Dictionary<EMTypes.MeteringPointSubType, Model.MeteringPointSubType> MeteringPointSubTypeMap = new()
    {
        { EMTypes.MeteringPointSubType.Physical, Model.MeteringPointSubType.Physical },
        { EMTypes.MeteringPointSubType.Virtual, Model.MeteringPointSubType.Virtual },
        { EMTypes.MeteringPointSubType.Calculated, Model.MeteringPointSubType.Calculated },
    };

    public static readonly Dictionary<EMTypes.ConnectionState, Model.ConnectionState> ConnectionStateMap = new()
    {
        { EMTypes.ConnectionState.NotUsed, Model.ConnectionState.NotUsed },
        { EMTypes.ConnectionState.ClosedDown, Model.ConnectionState.ClosedDown },
        { EMTypes.ConnectionState.New, Model.ConnectionState.New },
        { EMTypes.ConnectionState.Connected, Model.ConnectionState.Connected },
        { EMTypes.ConnectionState.Disconnected, Model.ConnectionState.Disconnected },
    };
}
