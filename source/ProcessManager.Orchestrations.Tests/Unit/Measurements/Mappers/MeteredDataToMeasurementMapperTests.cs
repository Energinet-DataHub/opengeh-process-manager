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
using Energinet.DataHub.ProcessManager.Orchestrations.Components.DataHub.Measurements.Mappers;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Measurements.Mappers;

public class MeteredDataToMeasurementMapperTests
{
    [Fact]
    public void Quality_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality, Quality>
        {
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality.NotAvailable, Quality.QMissing },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality.Estimated, Quality.QEstimated },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality.AsProvided, Quality.QMeasured },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Quality.Calculated, Quality.QCalculated },
        };

        MeteredDataToMeasurementMapper.Quality.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void Resolution_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Resolution, Resolution>
        {
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Resolution.QuarterHourly, Resolution.RPt15M }, { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.Resolution.Hourly, Resolution.RPt1H },
        };

        MeteredDataToMeasurementMapper.Resolution.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void MeasurementUnit_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<MeasurementUnit, Energinet.DataHub.Measurements.Contracts.Unit>
        {
            { MeasurementUnit.KilowattHour, Energinet.DataHub.Measurements.Contracts.Unit.UKwh },
            { MeasurementUnit.MegawattHour, Energinet.DataHub.Measurements.Contracts.Unit.UMwh },
            { MeasurementUnit.MegaVoltAmpereReactivePower, Energinet.DataHub.Measurements.Contracts.Unit.UMvar },
            { MeasurementUnit.KiloVoltAmpereReactiveHour, Energinet.DataHub.Measurements.Contracts.Unit.UKvarh },
            { MeasurementUnit.Kilowatt, Energinet.DataHub.Measurements.Contracts.Unit.UKw },
            { MeasurementUnit.MetricTon, Energinet.DataHub.Measurements.Contracts.Unit.UTonne },
        };

        MeteredDataToMeasurementMapper.MeasurementUnit.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void MeteringPointType_Mapping_ShouldBeCorrect()
    {
        var expectedMappings =
            new Dictionary<ProcessManager.Components.ValueObjects.MeteringPointType, DataHub.Measurements.Contracts.MeteringPointType>
            {
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.Consumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptConsumption
                },
                { ProcessManager.Components.ValueObjects.MeteringPointType.Production, DataHub.Measurements.Contracts.MeteringPointType.MptProduction },
                { ProcessManager.Components.ValueObjects.MeteringPointType.Exchange, DataHub.Measurements.Contracts.MeteringPointType.MptExchange },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.VeProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptVeProduction
                },
                { ProcessManager.Components.ValueObjects.MeteringPointType.Analysis, DataHub.Measurements.Contracts.MeteringPointType.MptAnalysis },
                { ProcessManager.Components.ValueObjects.MeteringPointType.NotUsed, DataHub.Measurements.Contracts.MeteringPointType.MptNotUsed },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.SurplusProductionGroup6,
                    DataHub.Measurements.Contracts.MeteringPointType.MptSurplusProductionGroup6
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.NetProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetProduction
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.SupplyToGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptSupplyToGrid
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.ConsumptionFromGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptConsumptionFromGrid
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.WholesaleServicesInformation,
                    DataHub.Measurements.Contracts.MeteringPointType.MptWholesaleServicesInformation
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.OwnProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOwnProduction
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.NetFromGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetFromGrid
                },
                { ProcessManager.Components.ValueObjects.MeteringPointType.NetToGrid, DataHub.Measurements.Contracts.MeteringPointType.MptNetToGrid },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.TotalConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptTotalConsumption
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.NetLossCorrection,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetLossCorrection
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.ElectricalHeating,
                    DataHub.Measurements.Contracts.MeteringPointType.MptElectricalHeating
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.NetConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetConsumption
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.OtherConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOtherConsumption
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.OtherProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOtherProduction
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.CapacitySettlement,
                    DataHub.Measurements.Contracts.MeteringPointType.MptEffectPayment
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.ExchangeReactiveEnergy,
                    DataHub.Measurements.Contracts.MeteringPointType.MptExchangeReactiveEnergy
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.CollectiveNetProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetProduction
                },
                {
                    ProcessManager.Components.ValueObjects.MeteringPointType.CollectiveNetConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetConsumption
                },
            };

        MeteredDataToMeasurementMapper.MeteringPointType.Should().BeEquivalentTo(expectedMappings);
    }
}
