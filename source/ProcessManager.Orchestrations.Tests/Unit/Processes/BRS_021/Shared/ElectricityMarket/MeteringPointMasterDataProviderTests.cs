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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ElectricityMarket.Integration.Models.Common;
using Energinet.DataHub.ElectricityMarket.Integration.Models.GridAreas;
using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ElectricityMarket.Integration.Models.ProcessDelegation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.Shared.
    ElectricityMarket;

[SuppressMessage(
    "StyleCop.CSharp.ReadabilityRules",
    "SA1114:Parameter list should follow declaration",
    Justification = "Allow comments to increase readability")]
public class MeteringPointMasterDataProviderTests
{
    private readonly MeteringPointMasterDataProvider _sut;

    public MeteringPointMasterDataProviderTests()
    {
        _sut = new MeteringPointMasterDataProvider(
            new ElectricityMarketViewsMock(),
            new Mock<ILogger<MeteringPointMasterDataProvider>>().Object);
    }

    [Fact]
    public async Task Given_NoMasterData_When_GetMasterData_Then_Empty()
    {
        (await _sut.GetMasterData("no-master-data-please", "2021-01-01T00:00:00Z", "2021-01-01T00:00:00Z"))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Given_MasterDataWithNoEnergySuppliers_When_GetMasterData_Then_NoMasterData()
    {
        var result = await _sut.GetMasterData(
            "no-energy-suppliers-please",
            "2021-01-01T00:00:00Z",
            "2021-01-02T00:00:00Z");

        var singleMasterData = result.Should().ContainSingle().Subject;
        singleMasterData.MeteringPointId.Value.Should().Be("no-energy-suppliers-please");
        singleMasterData.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
        singleMasterData.ValidTo.Should().Be(new DateTimeOffset(2021, 1, 2, 0, 0, 0, TimeSpan.Zero));
        singleMasterData.EnergySupplier.Should().BeNull();
    }

    [Fact]
    public async Task Given_MasterDataWithOneEnergySupplier_When_GetMasterData_Then_Single()
    {
        var meteringPointMasterData = await _sut.GetMasterData(
            "one-energy-supplier-please",
            "2021-01-01T00:00:00Z",
            "2021-02-01T00:00:00Z");

        var singleMasterData = meteringPointMasterData
            .Should()
            .ContainSingle().Subject;

        singleMasterData.MeteringPointId.Value.Should().Be("one-energy-supplier-please");
        singleMasterData.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
        singleMasterData.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
        singleMasterData.EnergySupplier.Should().NotBeNull();
        singleMasterData.EnergySupplier!.Value.Should().Be("1111111111111");
    }

    [Fact]
    public async Task Given_MasterDataWithTwoEnergySuppliers_When_GetMasterData_Then_ListOfTwo()
    {
        var meteringPointMasterData = await _sut.GetMasterData(
            "two-energy-suppliers-please",
            "2021-01-01T00:00:00Z",
            "2021-03-01T00:00:00Z");

        meteringPointMasterData
            .Should()
            .HaveCount(2);

        meteringPointMasterData.Should()
            .SatisfyRespectively(
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("two-energy-suppliers-please");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Should().NotBeNull();
                    first.EnergySupplier!.Value.Should().Be("1111111111111");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("two-energy-suppliers-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Should().NotBeNull();
                    second.EnergySupplier!.Value.Should().Be("2222222222222");
                });
    }

    [Fact]
    public async Task Given_TwoMasterDataWithTwoEnergySuppliers_When_GetMasterData_Then_ListOfFour()
    {
        var meteringPointMasterData = await _sut.GetMasterData(
            "two-master-data-with-two-energy-suppliers-please",
            "2021-01-01T00:00:00Z",
            "2021-05-01T00:00:00Z");

        meteringPointMasterData
            .Should()
            .HaveCount(4);

        meteringPointMasterData.Should()
            .SatisfyRespectively(
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("two-master-data-with-two-energy-suppliers-please");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Should().NotBeNull();
                    first.EnergySupplier!.Value.Should().Be("1111111111111");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("two-master-data-with-two-energy-suppliers-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Should().NotBeNull();
                    second.EnergySupplier!.Value.Should().Be("2222222222222");
                },
                third =>
                {
                    third.MeteringPointId.Value.Should().Be("two-master-data-with-two-energy-suppliers-please");
                    third.ValidFrom.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    third.ValidTo.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    third.EnergySupplier.Should().NotBeNull();
                    third.EnergySupplier!.Value.Should().Be("1111111111111");
                },
                fourth =>
                {
                    fourth.MeteringPointId.Value.Should().Be("two-master-data-with-two-energy-suppliers-please");
                    fourth.ValidFrom.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.ValidTo.Should().Be(new DateTimeOffset(2021, 5, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.EnergySupplier.Should().NotBeNull();
                    fourth.EnergySupplier!.Value.Should().Be("3333333333333");
                });
    }

    [Fact]
    public async Task
        Given_TwoMasterDataWithOneFaultyEnergySuppliers_When_GetMasterData_Then_AllMasterDataReturned()
    {
        var result = await _sut.GetMasterData(
            "faulty-two-master-data-please",
            "2021-01-01T00:00:00Z",
            "2021-05-01T00:00:00Z");

        result.Should()
            .SatisfyRespectively(
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("faulty-two-master-data-please");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Should().NotBeNull();
                    first.EnergySupplier!.Value.Should().Be("1111111111111");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("faulty-two-master-data-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Should().NotBeNull();
                    second.EnergySupplier!.Value.Should().Be("2222222222222");
                },
                noEnergySupplier =>
                {
                    noEnergySupplier.MeteringPointId.Value.Should().Be("faulty-two-master-data-please");
                    noEnergySupplier.ValidFrom.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    noEnergySupplier.ValidTo.Should().Be(new DateTimeOffset(2021, 5, 1, 0, 0, 0, TimeSpan.Zero));
                    noEnergySupplier.EnergySupplier.Should().BeNull();
                });
    }

    [Fact]
    public async Task Given_MasterDataWithMultipleParents_When_GetMasterData_Then_EnergySuppliersPickedFromCurrentParent()
    {
        var meteringPointMasterData = await _sut.GetMasterData(
            "two-parents-please",
            "2021-01-01T00:00:00Z",
            "2021-05-01T00:00:00Z");

        meteringPointMasterData
            .Should()
            .HaveCount(8)
            .And
            .SatisfyRespectively(
                // Master data from first parent
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("two-parents-please");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 1, 16, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Should().NotBeNull();
                    first.EnergySupplier!.Value.Should().Be("1212121212121");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("two-parents-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 16, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Should().NotBeNull();
                    second.EnergySupplier!.Value.Should().Be("2323232323232");
                },
                third =>
                {
                    third.MeteringPointId.Value.Should().Be("two-parents-please");
                    third.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    third.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 16, 0, 0, 0, TimeSpan.Zero));
                    third.EnergySupplier.Should().NotBeNull();
                    third.EnergySupplier!.Value.Should().Be("3434343434343");
                },
                fourth =>
                {
                    fourth.MeteringPointId.Value.Should().Be("two-parents-please");
                    fourth.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 16, 0, 0, 0, TimeSpan.Zero));
                    fourth.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.EnergySupplier.Should().NotBeNull();
                    fourth.EnergySupplier!.Value.Should().Be("4545454545454");
                },
                // Master data from second parent
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("two-parents-please");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 16, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Should().NotBeNull();
                    first.EnergySupplier!.Value.Should().Be("9090909090909");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("two-parents-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 3, 16, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Should().NotBeNull();
                    second.EnergySupplier!.Value.Should().Be("8989898989898");
                },
                third =>
                {
                    third.MeteringPointId.Value.Should().Be("two-parents-please");
                    third.ValidFrom.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    third.ValidTo.Should().Be(new DateTimeOffset(2021, 4, 16, 0, 0, 0, TimeSpan.Zero));
                    third.EnergySupplier.Should().NotBeNull();
                    third.EnergySupplier!.Value.Should().Be("7878787878787");
                },
                fourth =>
                {
                    fourth.MeteringPointId.Value.Should().Be("two-parents-please");
                    fourth.ValidFrom.Should().Be(new DateTimeOffset(2021, 4, 16, 0, 0, 0, TimeSpan.Zero));
                    fourth.ValidTo.Should().Be(new DateTimeOffset(2021, 5, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.EnergySupplier.Should().NotBeNull();
                    fourth.EnergySupplier!.Value.Should().Be("6767676767676");
                });
    }

    [Fact]
    public async Task
        Given_MasterDataWithAParentAndMasterDataWithoutAParent_When_GetMasterData_Then_EnergySuppliersPickedFromParentAndChild()
    {
        var meteringPointMasterData = await _sut.GetMasterData(
            "period-without-and-period-with-parent-please",
            "2021-01-01T00:00:00Z",
            "2021-05-01T00:00:00Z");

        meteringPointMasterData
            .Should()
            .HaveCount(6)
            .And
            .SatisfyRespectively(
                // Master data from parent
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("period-without-and-period-with-parent-please");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 1, 16, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Should().NotBeNull();
                    first.EnergySupplier!.Value.Should().Be("1212121212121");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("period-without-and-period-with-parent-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 16, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Should().NotBeNull();
                    second.EnergySupplier!.Value.Should().Be("2323232323232");
                },
                third =>
                {
                    third.MeteringPointId.Value.Should().Be("period-without-and-period-with-parent-please");
                    third.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    third.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 16, 0, 0, 0, TimeSpan.Zero));
                    third.EnergySupplier.Should().NotBeNull();
                    third.EnergySupplier!.Value.Should().Be("3434343434343");
                },
                fourth =>
                {
                    fourth.MeteringPointId.Value.Should().Be("period-without-and-period-with-parent-please");
                    fourth.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 16, 0, 0, 0, TimeSpan.Zero));
                    fourth.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.EnergySupplier.Should().NotBeNull();
                    fourth.EnergySupplier!.Value.Should().Be("4545454545454");
                },
                // Master data from metering point (no parent id in this period)
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("period-without-and-period-with-parent-please");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Should().NotBeNull();
                    first.EnergySupplier!.Value.Should().Be("1111111111111");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("period-without-and-period-with-parent-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 5, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Should().NotBeNull();
                    second.EnergySupplier!.Value.Should().Be("3333333333333");
                });
    }

    private class ElectricityMarketViewsMock : IElectricityMarketViews
    {
        /// <summary>
        /// The <paramref name="meteringPointIdentification"/> is used to determine which static data to return.
        /// The id describes the scenario the data is supposed to mimic.
        /// The <paramref name="interval"/> is not used in this mock and is ignored.
        /// </summary>
        public async Task<IEnumerable<MeteringPointMasterData>> GetMeteringPointMasterDataChangesAsync(
            MeteringPointIdentification meteringPointIdentification,
            Interval interval)
        {
            var masterDataTask = meteringPointIdentification.Value switch
            {
                "no-master-data-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>([]),
                "no-energy-suppliers-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("no-energy-suppliers-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 1, 2, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = null,
                    },
                ]),
                "one-energy-supplier-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("one-energy-supplier-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 2, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "1111111111111",
                    },
                ]),
                "two-energy-suppliers-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-energy-suppliers-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 2, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "1111111111111",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-energy-suppliers-please"),
                        ValidFrom = Instant.FromUtc(2021, 2, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 3, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "2222222222222",
                    },
                ]),
                "faulty-two-master-data-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("faulty-two-master-data-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 2, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "1111111111111",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("faulty-two-master-data-please"),
                        ValidFrom = Instant.FromUtc(2021, 2, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 3, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "2222222222222",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("faulty-two-master-data-please"),
                        ValidFrom = Instant.FromUtc(2021, 3, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 5, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = null,
                    },
                ]),
                "two-master-data-with-two-energy-suppliers-please" => Task
                    .FromResult<IEnumerable<MeteringPointMasterData>>(
                    [
                        new()
                        {
                            Identification =
                                new MeteringPointIdentification(
                                    "two-master-data-with-two-energy-suppliers-please"),
                            ValidFrom = Instant.FromUtc(
                                2021,
                                1,
                                1,
                                0,
                                0),
                            ValidTo = Instant.FromUtc(
                                2021,
                                2,
                                1,
                                0,
                                0),
                            GridAreaCode = new GridAreaCode("804"),
                            GridAccessProvider = "9999999999999",
                            ConnectionState =
                                ConnectionState.Connected,
                            Type = MeteringPointType.Consumption,
                            SubType = MeteringPointSubType.Physical,
                            Resolution = new Resolution("PT1H"),
                            Unit = MeasureUnit.kWh,
                            ProductId = ProductId.PowerActive,
                            ParentIdentification = null,
                            EnergySupplier = "1111111111111",
                        },
                         new()
                        {
                            Identification =
                                new MeteringPointIdentification(
                                    "two-master-data-with-two-energy-suppliers-please"),
                            ValidFrom = Instant.FromUtc(
                                2021,
                                2,
                                1,
                                0,
                                0),
                            ValidTo = Instant.FromUtc(
                                2021,
                                3,
                                1,
                                0,
                                0),
                            GridAreaCode = new GridAreaCode("804"),
                            GridAccessProvider = "9999999999999",
                            ConnectionState =
                                ConnectionState.Connected,
                            Type = MeteringPointType.Consumption,
                            SubType = MeteringPointSubType.Physical,
                            Resolution = new Resolution("PT1H"),
                            Unit = MeasureUnit.kWh,
                            ProductId = ProductId.PowerActive,
                            ParentIdentification = null,
                            EnergySupplier = "2222222222222",
                        },
                        new()
                        {
                            Identification =
                                new MeteringPointIdentification(
                                    "two-master-data-with-two-energy-suppliers-please"),
                            ValidFrom = Instant.FromUtc(
                                2021,
                                3,
                                1,
                                0,
                                0),
                            ValidTo = Instant.FromUtc(
                                2021,
                                4,
                                1,
                                0,
                                0),
                            GridAreaCode = new GridAreaCode("804"),
                            GridAccessProvider = "9999999999999",
                            ConnectionState =
                                ConnectionState.Connected,
                            Type = MeteringPointType.Consumption,
                            SubType = MeteringPointSubType.Physical,
                            Resolution = new Resolution("PT1H"),
                            Unit = MeasureUnit.kWh,
                            ProductId = ProductId.PowerActive,
                            ParentIdentification = null,
                            EnergySupplier = "1111111111111",
                        },
                        new()
                        {
                            Identification =
                                new MeteringPointIdentification(
                                    "two-master-data-with-two-energy-suppliers-please"),
                            ValidFrom = Instant.FromUtc(
                                2021,
                                4,
                                1,
                                0,
                                0),
                            ValidTo = Instant.FromUtc(
                                2021,
                                5,
                                1,
                                0,
                                0),
                            GridAreaCode = new GridAreaCode("804"),
                            GridAccessProvider = "9999999999999",
                            ConnectionState =
                                ConnectionState.Connected,
                            Type = MeteringPointType.Consumption,
                            SubType = MeteringPointSubType.Physical,
                            Resolution = new Resolution("PT1H"),
                            Unit = MeasureUnit.kWh,
                            ProductId = ProductId.PowerActive,
                            ParentIdentification = null,
                            EnergySupplier = "3333333333333",
                        },
                    ]),
                "two-parents-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-parents-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 2, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = new MeteringPointIdentification("parent-metering-point-id-one"),
                        EnergySupplier = "1111111111111",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-parents-please"),
                        ValidFrom = Instant.FromUtc(2021, 2, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 3, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = new MeteringPointIdentification("parent-metering-point-id-one"),
                        EnergySupplier = "2222222222222",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-parents-please"),
                        ValidFrom = Instant.FromUtc(2021, 3, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 4, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = new MeteringPointIdentification("parent-metering-point-id-two"),
                        EnergySupplier = "1111111111111",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-parents-please"),
                        ValidFrom = Instant.FromUtc(2021, 4, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 5, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = new MeteringPointIdentification("parent-metering-point-id-two"),
                        EnergySupplier = "3333333333333",
                    },
                ]),
                "period-without-and-period-with-parent-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification =
                            new MeteringPointIdentification("period-without-and-period-with-parent-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 2, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = new MeteringPointIdentification("parent-metering-point-id-one"),
                        EnergySupplier = "1111111111111",
                    },
                    new()
                    {
                        Identification =
                            new MeteringPointIdentification("period-without-and-period-with-parent-please"),
                        ValidFrom = Instant.FromUtc(2021, 2, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 3, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = new MeteringPointIdentification("parent-metering-point-id-one"),
                        EnergySupplier = "2222222222222",
                    },
                    new()
                    {
                        Identification =
                            new MeteringPointIdentification("period-without-and-period-with-parent-please"),
                        ValidFrom = Instant.FromUtc(2021, 3, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 4, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "1111111111111",
                    },
                    new()
                    {
                        Identification =
                            new MeteringPointIdentification("period-without-and-period-with-parent-please"),
                        ValidFrom = Instant.FromUtc(2021, 4, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 5, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "3333333333333",
                    },
                ]),
                "parent-metering-point-id-one" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("parent-metering-point-id-one"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 1, 16, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "1212121212121",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("parent-metering-point-id-one"),
                        ValidFrom = Instant.FromUtc(2021, 1, 16, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 2, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "2323232323232",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("parent-metering-point-id-one"),
                        ValidFrom = Instant.FromUtc(2021, 2, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 2, 16, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "3434343434343",
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("parent-metering-point-id-one"),
                        ValidFrom = Instant.FromUtc(2021, 2, 16, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 3, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "4545454545454",
                    },
                ]),
                "parent-metering-point-id-two" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("parent-metering-point-id-two"),
                        ValidFrom = Instant.FromUtc(2021, 3, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 3, 16, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "9090909090909",
                    },
                     new()
                    {
                        Identification = new MeteringPointIdentification("parent-metering-point-id-two"),
                        ValidFrom = Instant.FromUtc(2021, 3, 16, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 4, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "8989898989898",
                    },
                     new()
                    {
                        Identification = new MeteringPointIdentification("parent-metering-point-id-two"),
                        ValidFrom = Instant.FromUtc(2021, 4, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 4, 16, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "7878787878787",
                    },
                     new()
                    {
                        Identification = new MeteringPointIdentification("parent-metering-point-id-two"),
                        ValidFrom = Instant.FromUtc(2021, 4, 16, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 5, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("PT1H"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.PowerActive,
                        ParentIdentification = null,
                        EnergySupplier = "6767676767676",
                    },
                ]),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(meteringPointIdentification),
                    meteringPointIdentification,
                    null),
            };

            var masterData = await masterDataTask;

            // This period check follows the algorithm "bool overlap = a.start < b.end && b.start < a.end"
            // where a = md and b = interval.
            // See https://stackoverflow.com/questions/13513932/algorithm-to-detect-overlapping-periods for more info.
            return masterData
                .Where(md => md.ValidFrom < interval.End && interval.Start < md.ValidTo)
                .ToList();
        }

        public Task<ProcessDelegationDto?> GetProcessDelegationAsync(
            string actorNumber,
            EicFunction actorRole,
            string gridAreaCode,
            DelegatedProcess processType) =>
            throw new NotImplementedException();

        public Task<GridAreaOwnerDto?> GetGridAreaOwnerAsync(string gridAreaCode) =>
            throw new NotImplementedException();
    }
}
