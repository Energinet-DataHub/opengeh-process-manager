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

using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.Measurements.Mappers;
using FluentAssertions;
using MeasurementsTypes = Energinet.DataHub.Measurements.Contracts;
using PMTypes = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.SendMeasurements.Measurements.Mappers;

public class MeasurementsMapperTests
{
    [Fact]
    public void Quality_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<PMTypes.Quality, MeasurementsTypes.Quality>
        {
            { PMTypes.Quality.NotAvailable, MeasurementsTypes.Quality.QMissing },
            { PMTypes.Quality.Estimated, MeasurementsTypes.Quality.QEstimated },
            { PMTypes.Quality.AsProvided, MeasurementsTypes.Quality.QMeasured },
            { PMTypes.Quality.Calculated, MeasurementsTypes.Quality.QCalculated },
        };

        MeasurementsMapper.Quality.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void Resolution_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<PMTypes.Resolution, MeasurementsTypes.Resolution>
        {
            { PMTypes.Resolution.QuarterHourly, MeasurementsTypes.Resolution.RPt15M },
            { PMTypes.Resolution.Hourly, MeasurementsTypes.Resolution.RPt1H },
            { PMTypes.Resolution.Monthly, MeasurementsTypes.Resolution.RP1M },
        };

        MeasurementsMapper.Resolution.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void MeasurementUnit_Mapping_ShouldBeCorrect()
    {
        var expectedMappings = new Dictionary<PMTypes.MeasurementUnit, MeasurementsTypes.Unit>
        {
            { PMTypes.MeasurementUnit.KilowattHour, MeasurementsTypes.Unit.UKwh },
            { PMTypes.MeasurementUnit.MegawattHour, MeasurementsTypes.Unit.UMwh },
            { PMTypes.MeasurementUnit.MegaVoltAmpereReactivePower, MeasurementsTypes.Unit.UMvar },
            { PMTypes.MeasurementUnit.KiloVoltAmpereReactiveHour, MeasurementsTypes.Unit.UKvarh },
            { PMTypes.MeasurementUnit.Kilowatt, MeasurementsTypes.Unit.UKw },
            { PMTypes.MeasurementUnit.MetricTon, MeasurementsTypes.Unit.UTonne },
        };

        MeasurementsMapper.MeasurementUnit.Should().BeEquivalentTo(expectedMappings);
    }

    [Fact]
    public void MeteringPointType_Mapping_ShouldBeCorrect()
    {
        var expectedMappings =
            new Dictionary<PMTypes.MeteringPointType, MeasurementsTypes.MeteringPointType>
            {
                {
                    PMTypes.MeteringPointType.Consumption,
                    MeasurementsTypes.MeteringPointType.MptConsumption
                },
                { PMTypes.MeteringPointType.Production, MeasurementsTypes.MeteringPointType.MptProduction },
                { PMTypes.MeteringPointType.Exchange, MeasurementsTypes.MeteringPointType.MptExchange },
                {
                    PMTypes.MeteringPointType.VeProduction,
                    MeasurementsTypes.MeteringPointType.MptVeProduction
                },
                { PMTypes.MeteringPointType.Analysis, MeasurementsTypes.MeteringPointType.MptAnalysis },
                { PMTypes.MeteringPointType.NotUsed, MeasurementsTypes.MeteringPointType.MptNotUsed },
                {
                    PMTypes.MeteringPointType.SurplusProductionGroup6,
                    MeasurementsTypes.MeteringPointType.MptSurplusProductionGroup6
                },
                {
                    PMTypes.MeteringPointType.NetProduction,
                    MeasurementsTypes.MeteringPointType.MptNetProduction
                },
                {
                    PMTypes.MeteringPointType.SupplyToGrid,
                    MeasurementsTypes.MeteringPointType.MptSupplyToGrid
                },
                {
                    PMTypes.MeteringPointType.ConsumptionFromGrid,
                    MeasurementsTypes.MeteringPointType.MptConsumptionFromGrid
                },
                {
                    PMTypes.MeteringPointType.WholesaleServicesInformation,
                    MeasurementsTypes.MeteringPointType.MptWholesaleServicesInformation
                },
                {
                    PMTypes.MeteringPointType.OwnProduction,
                    MeasurementsTypes.MeteringPointType.MptOwnProduction
                },
                {
                    PMTypes.MeteringPointType.NetFromGrid,
                    MeasurementsTypes.MeteringPointType.MptNetFromGrid
                },
                {
                    PMTypes.MeteringPointType.NetToGrid,
                    MeasurementsTypes.MeteringPointType.MptNetToGrid
                },
                {
                    PMTypes.MeteringPointType.TotalConsumption,
                    MeasurementsTypes.MeteringPointType.MptTotalConsumption
                },
                {
                    PMTypes.MeteringPointType.NetLossCorrection,
                    MeasurementsTypes.MeteringPointType.MptNetLossCorrection
                },
                {
                    PMTypes.MeteringPointType.ElectricalHeating,
                    MeasurementsTypes.MeteringPointType.MptElectricalHeating
                },
                {
                    PMTypes.MeteringPointType.NetConsumption,
                    MeasurementsTypes.MeteringPointType.MptNetConsumption
                },
                {
                    PMTypes.MeteringPointType.OtherConsumption,
                    MeasurementsTypes.MeteringPointType.MptOtherConsumption
                },
                {
                    PMTypes.MeteringPointType.OtherProduction,
                    MeasurementsTypes.MeteringPointType.MptOtherProduction
                },
                {
                    PMTypes.MeteringPointType.CapacitySettlement,
                    MeasurementsTypes.MeteringPointType.MptCapacitySettlement
                },
                {
                    PMTypes.MeteringPointType.ExchangeReactiveEnergy,
                    MeasurementsTypes.MeteringPointType.MptExchangeReactiveEnergy
                },
                {
                    PMTypes.MeteringPointType.CollectiveNetProduction,
                    MeasurementsTypes.MeteringPointType.MptCollectiveNetProduction
                },
                {
                    PMTypes.MeteringPointType.CollectiveNetConsumption,
                    MeasurementsTypes.MeteringPointType.MptCollectiveNetConsumption
                },
                {
                    PMTypes.MeteringPointType.InternalUse,
                    MeasurementsTypes.MeteringPointType.MptInternalUse
                },
            };

        MeasurementsMapper.MeteringPointType.Should().BeEquivalentTo(expectedMappings);
    }
}
