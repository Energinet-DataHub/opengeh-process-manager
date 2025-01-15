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
using Energinet.DataHub.ProcessManager.Components.Measurements.Mappers;
using FluentAssertions;
using Xunit;
using DatahubMeasurementUnit = Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects.MeasurementUnit;
using DatahubMeteringPointType = Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects.MeteringPointType;
using DatahubQuality = Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects.Quality;
using DatahubResolution = Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects.Resolution;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Measurements.Mappers;

public class MeteredDataToMeasurementMapperTests
{
    [Fact]
    public void Quality_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<DatahubQuality, Quality>
        {
            { DatahubQuality.NotAvailable, Quality.QMissing },
            { DatahubQuality.Estimated, Quality.QEstimated },
            { DatahubQuality.AsProvided, Quality.QMeasured },
            { DatahubQuality.Calculated, Quality.QCalculated },
        };

        MeteredDataToMeasurementMapper.Quality.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void Resolution_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<DatahubResolution, Resolution>
        {
            { DatahubResolution.QuarterHourly, Resolution.RPt15M }, { DatahubResolution.Hourly, Resolution.RPt1H },
        };

        MeteredDataToMeasurementMapper.Resolution.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void MeasurementUnit_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<DatahubMeasurementUnit, Energinet.DataHub.Measurements.Contracts.Unit>
        {
            { DatahubMeasurementUnit.KilowattHour, Energinet.DataHub.Measurements.Contracts.Unit.UKwh },
            { DatahubMeasurementUnit.MegawattHour, Energinet.DataHub.Measurements.Contracts.Unit.UMwh },
            { DatahubMeasurementUnit.MegaVoltAmpereReactivePower, Energinet.DataHub.Measurements.Contracts.Unit.UMvar },
            { DatahubMeasurementUnit.KiloVoltAmpereReactiveHour, Energinet.DataHub.Measurements.Contracts.Unit.UKvarh },
            { DatahubMeasurementUnit.Kilowatt, Energinet.DataHub.Measurements.Contracts.Unit.UKw },
            { DatahubMeasurementUnit.MetricTon, Energinet.DataHub.Measurements.Contracts.Unit.UTonne },
        };

        MeteredDataToMeasurementMapper.MeasurementUnit.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void MeteringPointType_Mapping_ShouldBeCorrect()
    {
        var expectedMappings =
            new Dictionary<DatahubMeteringPointType, DataHub.Measurements.Contracts.MeteringPointType>
            {
                {
                    DatahubMeteringPointType.Consumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptConsumption
                },
                { DatahubMeteringPointType.Production, DataHub.Measurements.Contracts.MeteringPointType.MptProduction },
                { DatahubMeteringPointType.Exchange, DataHub.Measurements.Contracts.MeteringPointType.MptExchange },
                {
                    DatahubMeteringPointType.VeProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptVeProduction
                },
                { DatahubMeteringPointType.Analysis, DataHub.Measurements.Contracts.MeteringPointType.MptAnalysis },
                { DatahubMeteringPointType.NotUsed, DataHub.Measurements.Contracts.MeteringPointType.MptNotUsed },
                {
                    DatahubMeteringPointType.SurplusProductionGroup6,
                    DataHub.Measurements.Contracts.MeteringPointType.MptSurplusProductionGroup6
                },
                {
                    DatahubMeteringPointType.NetProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetProduction
                },
                {
                    DatahubMeteringPointType.SupplyToGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptSupplyToGrid
                },
                {
                    DatahubMeteringPointType.ConsumptionFromGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptConsumptionFromGrid
                },
                {
                    DatahubMeteringPointType.WholesaleServicesInformation,
                    DataHub.Measurements.Contracts.MeteringPointType.MptWholesaleServicesInformation
                },
                {
                    DatahubMeteringPointType.OwnProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOwnProduction
                },
                {
                    DatahubMeteringPointType.NetFromGrid,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetFromGrid
                },
                { DatahubMeteringPointType.NetToGrid, DataHub.Measurements.Contracts.MeteringPointType.MptNetToGrid },
                {
                    DatahubMeteringPointType.TotalConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptTotalConsumption
                },
                {
                    DatahubMeteringPointType.NetLossCorrection,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetLossCorrection
                },
                {
                    DatahubMeteringPointType.ElectricalHeating,
                    DataHub.Measurements.Contracts.MeteringPointType.MptElectricalHeating
                },
                {
                    DatahubMeteringPointType.NetConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptNetConsumption
                },
                {
                    DatahubMeteringPointType.OtherConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOtherConsumption
                },
                {
                    DatahubMeteringPointType.OtherProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptOtherProduction
                },
                {
                    DatahubMeteringPointType.CapacitySettlement,
                    DataHub.Measurements.Contracts.MeteringPointType.MptEffectPayment
                },
                {
                    DatahubMeteringPointType.ExchangeReactiveEnergy,
                    DataHub.Measurements.Contracts.MeteringPointType.MptExchangeReactiveEnergy
                },
                {
                    DatahubMeteringPointType.CollectiveNetProduction,
                    DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetProduction
                },
                {
                    DatahubMeteringPointType.CollectiveNetConsumption,
                    DataHub.Measurements.Contracts.MeteringPointType.MptCollectiveNetConsumption
                },
            };

        MeteredDataToMeasurementMapper.MeteringPointType.Should().BeEquivalentTo(expectedMappings);
    }
}
