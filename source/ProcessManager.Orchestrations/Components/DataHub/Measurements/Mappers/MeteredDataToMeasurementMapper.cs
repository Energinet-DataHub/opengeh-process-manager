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
using Energinet.DataHub.ProcessManager.Components.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Components.DataHub.Measurements.Mappers;

public static class MeteredDataToMeasurementMapper
{
    internal static Dictionary<ProcessManager.Components.ValueObjects.Quality, Energinet.DataHub.Measurements.Contracts.Quality> Quality { get; } = new()
    {
        { ProcessManager.Components.ValueObjects.Quality.NotAvailable, Energinet.DataHub.Measurements.Contracts.Quality.QMissing },
        { ProcessManager.Components.ValueObjects.Quality.Estimated, Energinet.DataHub.Measurements.Contracts.Quality.QEstimated },
        { ProcessManager.Components.ValueObjects.Quality.AsProvided, Energinet.DataHub.Measurements.Contracts.Quality.QMeasured },
        { ProcessManager.Components.ValueObjects.Quality.Calculated, Energinet.DataHub.Measurements.Contracts.Quality.QCalculated },
    };

    internal static Dictionary<ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Resolution, Resolution> Resolution { get; } = new()
    {
        { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Resolution.QuarterHourly, Energinet.DataHub.Measurements.Contracts.Resolution.RPt15M },
        { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Resolution.Hourly, Energinet.DataHub.Measurements.Contracts.Resolution.RPt1H },
    };

    internal static Dictionary<MeasurementUnit, Energinet.DataHub.Measurements.Contracts.Unit> MeasurementUnit { get; } = new()
    {
        { ProcessManager.Components.ValueObjects.MeasurementUnit.KilowattHour, Unit.UKwh },
        { ProcessManager.Components.ValueObjects.MeasurementUnit.MegawattHour, Unit.UMwh },
        { ProcessManager.Components.ValueObjects.MeasurementUnit.MegaVoltAmpereReactivePower, Unit.UMvar },
        { ProcessManager.Components.ValueObjects.MeasurementUnit.KiloVoltAmpereReactiveHour, Unit.UKvarh },
        { ProcessManager.Components.ValueObjects.MeasurementUnit.Kilowatt, Unit.UKw },
        { ProcessManager.Components.ValueObjects.MeasurementUnit.MetricTon, Unit.UTonne },
    };

    internal static Dictionary<ProcessManager.Components.ValueObjects.MeteringPointType, Energinet.DataHub.Measurements.Contracts.MeteringPointType> MeteringPointType { get; } = new()
        {
            { ProcessManager.Components.ValueObjects.MeteringPointType.Consumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptConsumption },
            { ProcessManager.Components.ValueObjects.MeteringPointType.Production, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptProduction },
            { ProcessManager.Components.ValueObjects.MeteringPointType.Exchange, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptExchange },
            { ProcessManager.Components.ValueObjects.MeteringPointType.VeProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptVeProduction },
            { ProcessManager.Components.ValueObjects.MeteringPointType.Analysis, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptAnalysis },
            { ProcessManager.Components.ValueObjects.MeteringPointType.NotUsed, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNotUsed },
            { ProcessManager.Components.ValueObjects.MeteringPointType.SurplusProductionGroup6, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptSurplusProductionGroup6 },
            { ProcessManager.Components.ValueObjects.MeteringPointType.NetProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetProduction },
            { ProcessManager.Components.ValueObjects.MeteringPointType.SupplyToGrid, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptSupplyToGrid },
            { ProcessManager.Components.ValueObjects.MeteringPointType.ConsumptionFromGrid, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptConsumptionFromGrid },
            { ProcessManager.Components.ValueObjects.MeteringPointType.WholesaleServicesInformation, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptWholesaleServicesInformation },
            { ProcessManager.Components.ValueObjects.MeteringPointType.OwnProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptOwnProduction },
            { ProcessManager.Components.ValueObjects.MeteringPointType.NetFromGrid, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetFromGrid },
            { ProcessManager.Components.ValueObjects.MeteringPointType.NetToGrid, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetToGrid },
            { ProcessManager.Components.ValueObjects.MeteringPointType.TotalConsumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptTotalConsumption },
            { ProcessManager.Components.ValueObjects.MeteringPointType.NetLossCorrection, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetLossCorrection },
            { ProcessManager.Components.ValueObjects.MeteringPointType.ElectricalHeating, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptElectricalHeating },
            { ProcessManager.Components.ValueObjects.MeteringPointType.NetConsumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetConsumption },
            { ProcessManager.Components.ValueObjects.MeteringPointType.OtherConsumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptOtherConsumption },
            { ProcessManager.Components.ValueObjects.MeteringPointType.OtherProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptOtherProduction },
            { ProcessManager.Components.ValueObjects.MeteringPointType.CapacitySettlement, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptEffectPayment },
            { ProcessManager.Components.ValueObjects.MeteringPointType.ExchangeReactiveEnergy, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptExchangeReactiveEnergy },
            { ProcessManager.Components.ValueObjects.MeteringPointType.CollectiveNetProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetProduction },
            { ProcessManager.Components.ValueObjects.MeteringPointType.CollectiveNetConsumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetConsumption },
        };
}
