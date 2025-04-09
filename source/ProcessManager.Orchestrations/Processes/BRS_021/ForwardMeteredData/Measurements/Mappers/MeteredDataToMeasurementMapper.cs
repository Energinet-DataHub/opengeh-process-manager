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

using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Mappers;
using MeasurementTypes = Energinet.DataHub.Measurements.Contracts;
using PMTypes = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Mappers;

public static class MeteredDataToMeasurementMapper
{
    internal static ValueObjectsMap<PMTypes.Quality, MeasurementTypes.Quality> Quality { get; } = new()
    {
        { PMTypes.Quality.NotAvailable, MeasurementTypes.Quality.QMissing },
        { PMTypes.Quality.Estimated, MeasurementTypes.Quality.QEstimated },
        { PMTypes.Quality.AsProvided, MeasurementTypes.Quality.QMeasured },
        { PMTypes.Quality.Calculated, MeasurementTypes.Quality.QCalculated },
    };

    internal static ValueObjectsMap<PMTypes.Resolution, MeasurementTypes.Resolution> Resolution { get; } = new()
    {
        { PMTypes.Resolution.QuarterHourly, MeasurementTypes.Resolution.RPt15M },
        { PMTypes.Resolution.Hourly, MeasurementTypes.Resolution.RPt1H },
    };

    internal static ValueObjectsMap<PMTypes.MeasurementUnit, MeasurementTypes.Unit> MeasurementUnit { get; } = new()
    {
        { PMTypes.MeasurementUnit.KilowattHour, MeasurementTypes.Unit.UKwh },
        { PMTypes.MeasurementUnit.MegawattHour, MeasurementTypes.Unit.UMwh },
        { PMTypes.MeasurementUnit.MegaVoltAmpereReactivePower, MeasurementTypes.Unit.UMvar },
        { PMTypes.MeasurementUnit.KiloVoltAmpereReactiveHour, MeasurementTypes.Unit.UKvarh },
        { PMTypes.MeasurementUnit.Kilowatt, MeasurementTypes.Unit.UKw },
        { PMTypes.MeasurementUnit.MetricTon, MeasurementTypes.Unit.UTonne },
    };

    internal static ValueObjectsMap<PMTypes.MeteringPointType, MeasurementTypes.MeteringPointType> MeteringPointType { get; } = new()
        {
            { PMTypes.MeteringPointType.Consumption, MeasurementTypes.MeteringPointType.MptConsumption },
            { PMTypes.MeteringPointType.Production, MeasurementTypes.MeteringPointType.MptProduction },
            { PMTypes.MeteringPointType.Exchange, MeasurementTypes.MeteringPointType.MptExchange },
            { PMTypes.MeteringPointType.VeProduction, MeasurementTypes.MeteringPointType.MptVeProduction },
            { PMTypes.MeteringPointType.Analysis, MeasurementTypes.MeteringPointType.MptAnalysis },
            { PMTypes.MeteringPointType.NotUsed, MeasurementTypes.MeteringPointType.MptNotUsed },
            { PMTypes.MeteringPointType.SurplusProductionGroup6, MeasurementTypes.MeteringPointType.MptSurplusProductionGroup6 },
            { PMTypes.MeteringPointType.NetProduction, MeasurementTypes.MeteringPointType.MptNetProduction },
            { PMTypes.MeteringPointType.SupplyToGrid, MeasurementTypes.MeteringPointType.MptSupplyToGrid },
            { PMTypes.MeteringPointType.ConsumptionFromGrid, MeasurementTypes.MeteringPointType.MptConsumptionFromGrid },
            { PMTypes.MeteringPointType.WholesaleServicesInformation, MeasurementTypes.MeteringPointType.MptWholesaleServicesInformation },
            { PMTypes.MeteringPointType.OwnProduction, MeasurementTypes.MeteringPointType.MptOwnProduction },
            { PMTypes.MeteringPointType.NetFromGrid, MeasurementTypes.MeteringPointType.MptNetFromGrid },
            { PMTypes.MeteringPointType.NetToGrid, MeasurementTypes.MeteringPointType.MptNetToGrid },
            { PMTypes.MeteringPointType.TotalConsumption, MeasurementTypes.MeteringPointType.MptTotalConsumption },
            { PMTypes.MeteringPointType.NetLossCorrection, MeasurementTypes.MeteringPointType.MptNetLossCorrection },
            { PMTypes.MeteringPointType.ElectricalHeating, MeasurementTypes.MeteringPointType.MptElectricalHeating },
            { PMTypes.MeteringPointType.NetConsumption, MeasurementTypes.MeteringPointType.MptNetConsumption },
            { PMTypes.MeteringPointType.OtherConsumption, MeasurementTypes.MeteringPointType.MptOtherConsumption },
            { PMTypes.MeteringPointType.OtherProduction, MeasurementTypes.MeteringPointType.MptOtherProduction },
            { PMTypes.MeteringPointType.CapacitySettlement, MeasurementTypes.MeteringPointType.MptCapacitySettlement },
            { PMTypes.MeteringPointType.ExchangeReactiveEnergy, MeasurementTypes.MeteringPointType.MptExchangeReactiveEnergy },
            { PMTypes.MeteringPointType.CollectiveNetProduction, MeasurementTypes.MeteringPointType.MptCollectiveNetProduction },
            { PMTypes.MeteringPointType.CollectiveNetConsumption, MeasurementTypes.MeteringPointType.MptCollectiveNetConsumption },
            { PMTypes.MeteringPointType.InternalUse, MeasurementTypes.MeteringPointType.MptInternalUse },
        };
}
