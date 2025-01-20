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
        var expectedMappings = new Dictionary<ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit, Energinet.DataHub.Measurements.Contracts.Unit>
        {
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.KilowattHour, Energinet.DataHub.Measurements.Contracts.Unit.UKwh },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.MegawattHour, Energinet.DataHub.Measurements.Contracts.Unit.UMwh },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.MegaVoltAmpereReactivePower, Energinet.DataHub.Measurements.Contracts.Unit.UMvar },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.KiloVoltAmpereReactiveHour, Energinet.DataHub.Measurements.Contracts.Unit.UKvarh },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.Kilowatt, Energinet.DataHub.Measurements.Contracts.Unit.UKw },
            { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeasurementUnit.MetricTon, Energinet.DataHub.Measurements.Contracts.Unit.UTonne },
        };

        MeteredDataToMeasurementMapper.MeasurementUnit.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void MeteringPointType_Mapping_ShouldBeCorrect()
    {
        var expectedMappings =
            new Dictionary<ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType, DataHub.Measurements.Contracts.MeteringPointType>
            {
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Consumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptConsumption
                },
                { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Production, DataHub.Measurements.Contracts.MeteringPointType.MptProduction },
                { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Exchange, DataHub.Measurements.Contracts.MeteringPointType.MptExchange },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.VeProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptVeProduction
                },
                { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.Analysis, DataHub.Measurements.Contracts.MeteringPointType.MptAnalysis },
                { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NotUsed, DataHub.Measurements.Contracts.MeteringPointType.MptNotUsed },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.SurplusProductionGroup6,
                    DataHub.Measurements.Contracts.MeteringPointType.MptSurplusProductionGroup6
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetProduction
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.SupplyToGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptSupplyToGrid
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.ConsumptionFromGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptConsumptionFromGrid
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.WholesaleServicesInformation,
                    DataHub.Measurements.Contracts.MeteringPointType.MptWholesaleServicesInformation
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.OwnProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOwnProduction
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetFromGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetFromGrid
                },
                { ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetToGrid, DataHub.Measurements.Contracts.MeteringPointType.MptNetToGrid },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.TotalConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptTotalConsumption
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetLossCorrection,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetLossCorrection
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.ElectricalHeating,
                    DataHub.Measurements.Contracts.MeteringPointType.MptElectricalHeating
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.NetConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetConsumption
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.OtherConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOtherConsumption
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.OtherProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOtherProduction
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.CapacitySettlement,
                    DataHub.Measurements.Contracts.MeteringPointType.MptEffectPayment
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.ExchangeReactiveEnergy,
                    DataHub.Measurements.Contracts.MeteringPointType.MptExchangeReactiveEnergy
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.CollectiveNetProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetProduction
                },
                {
                    ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.CollectiveNetConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetConsumption
                },
            };

        MeteredDataToMeasurementMapper.MeteringPointType.Should().BeEquivalentTo(expectedMappings);
    }
}
