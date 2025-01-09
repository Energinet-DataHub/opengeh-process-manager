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

using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects;
using Quality = Energinet.DataHub.Measurements.Contracts.Quality;
using Resolution = Energinet.DataHub.Measurements.Contracts.Resolution;

namespace Energinet.DataHub.ProcessManager.Components.Measurements.Mappers;

public static class MeteredDataToMeasurementMapper
{
    internal static Dictionary<Datahub.ValueObjects.Quality, Quality> Quality { get; } = new()
    {
        { Datahub.ValueObjects.Quality.NotAvailable, DataHub.Measurements.Contracts.Quality.QMissing },
        { Datahub.ValueObjects.Quality.Estimated, DataHub.Measurements.Contracts.Quality.QEstimated },
        { Datahub.ValueObjects.Quality.AsProvided, DataHub.Measurements.Contracts.Quality.QMeasured },
        { Datahub.ValueObjects.Quality.Calculated, DataHub.Measurements.Contracts.Quality.QCalculated },
    };

    internal static Dictionary<Datahub.ValueObjects.Resolution, Resolution> Resolution { get; } = new()
    {
        { Datahub.ValueObjects.Resolution.QuarterHourly, DataHub.Measurements.Contracts.Resolution.RPt15M },
        { Datahub.ValueObjects.Resolution.Hourly, DataHub.Measurements.Contracts.Resolution.RPt1H },
    };

    internal static Dictionary<MeasurementUnit, Unit> MeasurementUnit { get; } = new()
    {
        { Datahub.ValueObjects.MeasurementUnit.KilowattHour, Unit.UKwh },
        { Datahub.ValueObjects.MeasurementUnit.MegawattHour, Unit.UMwh },
        { Datahub.ValueObjects.MeasurementUnit.MegaVoltAmpereReactivePower, Unit.UMvarh },
        { Datahub.ValueObjects.MeasurementUnit.KiloVoltAmpereReactiveHour, Unit.UKvarh },
        { Datahub.ValueObjects.MeasurementUnit.Kilowatt, Unit.UKw },
        { Datahub.ValueObjects.MeasurementUnit.MetricTon, Unit.UT },
        { Datahub.ValueObjects.MeasurementUnit.KiloVoltAmpereReactiveHour, Unit.UK3 },
    };

    internal static Dictionary<Datahub.ValueObjects.MeteringPointType, DataHub.Measurements.Contracts.MeteringPointType> MeteringPointType { get; } = new()
        {
            { Datahub.ValueObjects.MeteringPointType.Consumption, DataHub.Measurements.Contracts.MeteringPointType.MptConsumption },
            { Datahub.ValueObjects.MeteringPointType.Production, DataHub.Measurements.Contracts.MeteringPointType.MptProduction },
            { Datahub.ValueObjects.MeteringPointType.Exchange, DataHub.Measurements.Contracts.MeteringPointType.MptExchange },
            { Datahub.ValueObjects.MeteringPointType.VeProduction, DataHub.Measurements.Contracts.MeteringPointType.MptVeProduction },
            { Datahub.ValueObjects.MeteringPointType.Analysis, DataHub.Measurements.Contracts.MeteringPointType.MptAnalysis },
            { Datahub.ValueObjects.MeteringPointType.NotUsed, DataHub.Measurements.Contracts.MeteringPointType.MptNotUsed },
            { Datahub.ValueObjects.MeteringPointType.SurplusProductionGroup6, DataHub.Measurements.Contracts.MeteringPointType.MptSurplusProductionGroup6 },
            { Datahub.ValueObjects.MeteringPointType.NetProduction, DataHub.Measurements.Contracts.MeteringPointType.MptNetProduction },
            { Datahub.ValueObjects.MeteringPointType.SupplyToGrid, DataHub.Measurements.Contracts.MeteringPointType.MptSupplyToGrid },
            { Datahub.ValueObjects.MeteringPointType.ConsumptionFromGrid, DataHub.Measurements.Contracts.MeteringPointType.MptConsumptionFromGrid },
            { Datahub.ValueObjects.MeteringPointType.WholesaleServicesInformation, DataHub.Measurements.Contracts.MeteringPointType.MptWholesaleServicesInformation },
            { Datahub.ValueObjects.MeteringPointType.OwnProduction, DataHub.Measurements.Contracts.MeteringPointType.MptOwnProduction },
            { Datahub.ValueObjects.MeteringPointType.NetFromGrid, DataHub.Measurements.Contracts.MeteringPointType.MptNetFromGrid },
            { Datahub.ValueObjects.MeteringPointType.NetToGrid, DataHub.Measurements.Contracts.MeteringPointType.MptNetToGrid },
            { Datahub.ValueObjects.MeteringPointType.TotalConsumption, DataHub.Measurements.Contracts.MeteringPointType.MptTotalConsumption },
            { Datahub.ValueObjects.MeteringPointType.NetLossCorrection, DataHub.Measurements.Contracts.MeteringPointType.MptNetLossCorrection },
            { Datahub.ValueObjects.MeteringPointType.ElectricalHeating, DataHub.Measurements.Contracts.MeteringPointType.MptElectricalHeating },
            { Datahub.ValueObjects.MeteringPointType.NetConsumption, DataHub.Measurements.Contracts.MeteringPointType.MptNetConsumption },
            { Datahub.ValueObjects.MeteringPointType.OtherConsumption, DataHub.Measurements.Contracts.MeteringPointType.MptOtherConsumption },
            { Datahub.ValueObjects.MeteringPointType.OtherProduction, DataHub.Measurements.Contracts.MeteringPointType.MptOtherProduction },
            { Datahub.ValueObjects.MeteringPointType.CapacitySettlement, DataHub.Measurements.Contracts.MeteringPointType.MptEffectPayment },
            { Datahub.ValueObjects.MeteringPointType.ExchangeReactiveEnergy, DataHub.Measurements.Contracts.MeteringPointType.MptExchangeReactiveEnergy },
            { Datahub.ValueObjects.MeteringPointType.CollectiveNetProduction, DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetProduction },
            { Datahub.ValueObjects.MeteringPointType.CollectiveNetConsumption, DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetConsumption },
        };
}
