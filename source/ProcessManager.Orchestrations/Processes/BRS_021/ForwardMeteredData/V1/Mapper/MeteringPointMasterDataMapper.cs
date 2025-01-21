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

using Energinet.DataHub.ElectricityMarket.Integration;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Mapper;

public static class MeteringPointMasterDataMapper
{
    public static readonly Dictionary<MeteringPointType, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType> MeteringPointTypeMap = new()
    {
        { MeteringPointType.Consumption, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Consumption },
        { MeteringPointType.Production, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Production },
        { MeteringPointType.Exchange, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Exchange },
    };

    public static readonly Dictionary<MeasureUnit, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit> MeasureUnitMap = new()
    {
        { MeasureUnit.Ampere, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.Ampere },
        { MeasureUnit.STK, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.Pieces },
        { MeasureUnit.kVArh, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.KiloVoltAmpereReactiveHour },
        { MeasureUnit.kWh, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.KilowattHour },
        { MeasureUnit.kW, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.Kilowatt },
        { MeasureUnit.MW, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.Megawatt },
        { MeasureUnit.MWh, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.MegawattHour },
        { MeasureUnit.Tonne, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.MetricTon },
        { MeasureUnit.MVAr, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.MegaVoltAmpereReactivePower },
        { MeasureUnit.DanishTariffCode, Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.DanishTariffCode },
    };

    public static readonly Dictionary<MeteringPointSubType, Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType> MeteringPointSubTypeMap = new()
    {
        { MeteringPointSubType.Physical, Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType.Physical },
        { MeteringPointSubType.Virtual, Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType.Virtual },
        { MeteringPointSubType.Calculated, Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType.Calculated },
    };

    public static readonly Dictionary<ConnectionState, Model.ConnectionState> ConnectionStateMap = new()
    {
        { ConnectionState.NotUsed, Model.ConnectionState.NotUsed },
        { ConnectionState.ClosedDown, Model.ConnectionState.ClosedDown },
        { ConnectionState.New, Model.ConnectionState.New },
        { ConnectionState.Connected, Model.ConnectionState.Connected },
        { ConnectionState.Disconnected, Model.ConnectionState.Disconnected },
    };
}
