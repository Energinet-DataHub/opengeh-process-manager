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
    internal static Dictionary<ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality, Energinet.DataHub.Measurements.Contracts.Quality> Quality { get; } = new()
    {
        { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality.NotAvailable, Energinet.DataHub.Measurements.Contracts.Quality.QMissing },
        { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality.Estimated, Energinet.DataHub.Measurements.Contracts.Quality.QEstimated },
        { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality.AsProvided, Energinet.DataHub.Measurements.Contracts.Quality.QMeasured },
        { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality.Calculated, Energinet.DataHub.Measurements.Contracts.Quality.QCalculated },
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

    internal static Dictionary<ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType, Energinet.DataHub.Measurements.Contracts.MeteringPointType> MeteringPointType { get; } = new()
        {
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Consumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptConsumption },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Production, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptProduction },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Exchange, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptExchange },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.VeProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptVeProduction },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Analysis, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptAnalysis },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NotUsed, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNotUsed },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.SurplusProductionGroup6, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptSurplusProductionGroup6 },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetProduction },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.SupplyToGrid, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptSupplyToGrid },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.ConsumptionFromGrid, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptConsumptionFromGrid },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.WholesaleServicesInformation, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptWholesaleServicesInformation },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.OwnProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptOwnProduction },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetFromGrid, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetFromGrid },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetToGrid, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetToGrid },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.TotalConsumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptTotalConsumption },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetLossCorrection, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetLossCorrection },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.ElectricalHeating, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptElectricalHeating },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetConsumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptNetConsumption },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.OtherConsumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptOtherConsumption },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.OtherProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptOtherProduction },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.CapacitySettlement, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptEffectPayment },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.ExchangeReactiveEnergy, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptExchangeReactiveEnergy },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.CollectiveNetProduction, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetProduction },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.CollectiveNetConsumption, Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetConsumption },
        };
}
