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
using Energinet.DataHub.ElectricityMarket.Integration.Models.Common;
using Energinet.DataHub.ElectricityMarket.Integration.Models.GridAreas;
using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ElectricityMarket.Integration.Models.ProcessDelegation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.
    ElectricityMarket;

public class MeteringPointMasterDataProviderTests
{
    private readonly ElectricityMarketViewsMock _electricityMarketViews;
    private readonly MeteringPointMasterDataProvider _sut;

    public MeteringPointMasterDataProviderTests()
    {
        _electricityMarketViews = new ElectricityMarketViewsMock();
        _sut = new MeteringPointMasterDataProvider(
            _electricityMarketViews,
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
    public async Task Given_MasterDataWithNoEnergySuppliers_When_GetMasterData_Then_Error()
    {
        var act = async () => await _sut.GetMasterData(
            "no-energy-suppliers-please",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should()
            .ThrowAsync<Exception>()
            .WithMessage(
                "No energy suppliers found for metering point 'no-energy-suppliers-please' in period 2021-01-01T00:00:00Z--2021-01-01T00:00:00Z.");
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
        singleMasterData.EnergySupplier.Value.Should().Be("1111111111111");
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
                    first.EnergySupplier.Value.Should().Be("1111111111111");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("two-energy-suppliers-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Value.Should().Be("2222222222222");
                });
    }

    [Fact]
    public async Task Given_TwoMasterDataWithTwoEnergySuppliers_When_GetMasterData_Then_ListOfFour()
    {
        _electricityMarketViews.MeteringPointMasterDataProvider = () => GetMeteringPointMasterData();

        var meteringPointMasterData = await _sut.GetMasterData(
            "custom-metering-point-master-data",
            "2021-01-01T00:00:00Z",
            "2021-01-05T00:00:00Z");

        meteringPointMasterData
            .Should()
            .HaveCount(4);

        meteringPointMasterData.Should()
            .SatisfyRespectively(
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("custom-metering-point-master-data");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Value.Should().Be("1111111111111");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("custom-metering-point-master-data");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Value.Should().Be("2222222222222");
                },
                third =>
                {
                    third.MeteringPointId.Value.Should().Be("custom-metering-point-master-data");
                    third.ValidFrom.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    third.ValidTo.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    third.EnergySupplier.Value.Should().Be("1111111111111");
                },
                fourth =>
                {
                    fourth.MeteringPointId.Value.Should().Be("custom-metering-point-master-data");
                    fourth.ValidFrom.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.ValidTo.Should().Be(new DateTimeOffset(2021, 5, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.EnergySupplier.Value.Should().Be("3333333333333");
                });
    }

    [Fact]
    public async Task Given_TwoMasterDataWithOneFaultyEnergySuppliers_When_GetMasterData_Then_Error()
    {
        var act = async () => await _sut.GetMasterData(
            "faulty-two-master-data-please",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should()
            .ThrowAsync<Exception>()
            .WithMessage(
                "No energy suppliers found for metering point 'faulty-two-master-data-please' in period 2021-03-01T00:00:00Z--2021-05-01T00:00:00Z.");
    }

    [Fact(Skip = "Awaiting clarification wrt the expected behavior")]
    public async Task Given_MasterDataWithEnergySupplierAndMasterDataWithMissingEnergySupplier_When_GetMasterData_Then_Error()
    {
        _electricityMarketViews.MeteringPointMasterDataProvider = () => GetMeteringPointMasterData(gapInDates: true);

        var act = async () => await _sut.GetMasterData(
            "custom-metering-point-master-data",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should().ThrowAsync<Exception>().WithMessage("Metering point master data is not consistent");
    }

    [Fact(Skip = "Awaiting clarification wrt the expected behavior")]
    public async Task Given_MasterDataWithOverlappingEnergySuppliers_When_GetMasterData_Then_Error()
    {
        _electricityMarketViews.MeteringPointMasterDataProvider =
            () => GetMeteringPointMasterData(overlapInDates: true);

        var act = async () => await _sut.GetMasterData(
            "custom-metering-point-master-data",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should().ThrowAsync<Exception>().WithMessage("Metering point master data is not consistent");
    }

    [Fact(Skip = "Awaiting clarification wrt the expected behavior")]
    public async Task Given_MasterDataWithNonMatchingDatesForEnergySuppliers_When_GetMasterData_Then_Error()
    {
        _electricityMarketViews.MeteringPointMasterDataProvider =
            () => GetMeteringPointMasterData(datesNotMatching: true);

        var act = async () => await _sut.GetMasterData(
            "custom-metering-point-master-data",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should()
            .ThrowAsync<Exception>()
            .WithMessage(
                "The interval of the energy suppliers (2021-01-01T00:00:00Z--2021-03-01T00:00:00Z) does not match the master data interval (2021-01-01T00:00:00Z--2021-03-02T00:00:00Z).");
    }

    [Fact(Skip = "Awaiting clarification wrt the expected behavior")]
    public async Task Given_MasterDataWithInconsistentMeteringPointType_When_GetMasterData_Then_Error()
    {
        _electricityMarketViews.MeteringPointMasterDataProvider =
            () => GetMeteringPointMasterData(changeToType: true);

        var act = async () => await _sut.GetMasterData(
            "custom-metering-point-master-data",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should()
            .ThrowAsync<Exception>()
            .WithMessage("MeteringPointType 'Production' is not equal to previous MeteringPointType 'Consumption'");
    }

    [Fact(Skip = "Awaiting clarification wrt the expected behavior")]
    public async Task Given_MasterDataWithChangeToMeasurementUnit_When_GetMasterData_Then_Error()
    {
        _electricityMarketViews.MeteringPointMasterDataProvider =
            () => GetMeteringPointMasterData(changeToMeasurementUnit: true);

        var act = async () => await _sut.GetMasterData(
            "custom-metering-point-master-data",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should()
            .ThrowAsync<Exception>()
            .WithMessage("MeasurementUnit 'kWh' is not equal to previous MeasurementUnit 'MVAr'");
    }

    [Fact(Skip = "Awaiting clarification wrt the expected behavior")]
    public async Task Given_MasterDataWithChangeToProductId_When_GetMasterData_Then_Error()
    {
        _electricityMarketViews.MeteringPointMasterDataProvider =
            () => GetMeteringPointMasterData(changeToProductId: true);

        var act = async () => await _sut.GetMasterData(
            "custom-metering-point-master-data",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should()
            .ThrowAsync<Exception>()
            .WithMessage("ProductId 'Tariff' is not equal to previous ProductId 'EnergyActivate'");
    }

    private IReadOnlyCollection<MeteringPointMasterData> GetMeteringPointMasterData(
        bool gapInDates = false,
        bool overlapInDates = false,
        bool datesNotMatching = false,
        bool changeToType = false,
        bool changeToMeasurementUnit = false,
        bool changeToProductId = false) =>
    [
        new()
        {
            Identification = new MeteringPointIdentification("custom-metering-point-master-data"),
            ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
            ValidTo = Instant.FromUtc(2021, 3, datesNotMatching ? 2 : 1, 0, 0),
            GridAreaCode = new GridAreaCode("804"),
            GridAccessProvider = "9999999999999",
            ConnectionState = ConnectionState.Connected,
            Type = MeteringPointType.Consumption,
            SubType = MeteringPointSubType.Physical,
            Resolution = new Resolution("Hourly"),
            Unit = changeToMeasurementUnit ? MeasureUnit.MVAr : MeasureUnit.kWh,
            ProductId = ProductId.EnergyActivate,
            ParentIdentification = null,
            EnergySuppliers =
            [
                new()
                {
                    Identification = new MeteringPointIdentification("custom-metering-point-master-data"),
                    EnergySupplier = "1111111111111",
                    StartDate = Instant.FromUtc(2021, 1, 1, 0, 0),
                    EndDate = Instant.FromUtc(2021, 2, overlapInDates ? 2 : 1, 0, 0),
                },
                new()
                {
                    Identification = new MeteringPointIdentification("custom-metering-point-master-data"),
                    EnergySupplier = "2222222222222",
                    StartDate = Instant.FromUtc(2021, 2, 1, 0, 0),
                    EndDate = Instant.FromUtc(2021, 3, 1, 0, 0),
                },
            ],
        },
        new()
        {
            Identification = new MeteringPointIdentification("custom-metering-point-master-data"),
            ValidFrom = Instant.FromUtc(2021, 3, 1, 0, 0),
            ValidTo = Instant.FromUtc(2021, 5, 1, 0, 0),
            GridAreaCode = new GridAreaCode("804"),
            GridAccessProvider = "9999999999999",
            ConnectionState = ConnectionState.Connected,
            Type = changeToType ? MeteringPointType.Production : MeteringPointType.Consumption,
            SubType = MeteringPointSubType.Physical,
            Resolution = new Resolution("Hourly"),
            Unit = MeasureUnit.kWh,
            ProductId = changeToProductId ? ProductId.Tariff : ProductId.EnergyActivate,
            ParentIdentification = null,
            EnergySuppliers =
            [
                new()
                {
                    Identification = new MeteringPointIdentification("custom-metering-point-master-data"),
                    EnergySupplier = "1111111111111",
                    StartDate = Instant.FromUtc(2021, 3, 1, 0, 0),
                    EndDate = Instant.FromUtc(2021, 4, 1, 0, 0),
                },
                new()
                {
                    Identification = new MeteringPointIdentification("custom-metering-point-master-data"),
                    EnergySupplier = "3333333333333",
                    StartDate = Instant.FromUtc(2021, 4, gapInDates ? 2 : 1, 0, 0),
                    EndDate = Instant.FromUtc(2021, 5, 1, 0, 0),
                },
            ],
        },
    ];

    private class ElectricityMarketViewsMock : IElectricityMarketViews
    {
        public Func<IEnumerable<MeteringPointMasterData>> MeteringPointMasterDataProvider { get; set; } =
            () => [];

        public Task<IEnumerable<MeteringPointMasterData>> GetMeteringPointMasterDataChangesAsync(
            MeteringPointIdentification meteringPointIdentification,
            Interval interval)
        {
            return meteringPointIdentification.Value switch
            {
                "custom-metering-point-master-data" => Task.FromResult(MeteringPointMasterDataProvider()),
                "no-master-data-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>([]),
                "no-energy-suppliers-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("no-energy-suppliers-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 1, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("Hourly"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.EnergyActivate,
                        ParentIdentification = null,
                        EnergySuppliers = [],
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
                        Resolution = new Resolution("Hourly"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.EnergyActivate,
                        ParentIdentification = null,
                        EnergySuppliers =
                        [
                            new()
                            {
                                Identification = new MeteringPointIdentification("one-energy-supplier-please"),
                                EnergySupplier = "1111111111111",
                                StartDate = Instant.FromUtc(2021, 1, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 2, 1, 0, 0),
                            },
                        ],
                    },
                ]),
                "two-energy-suppliers-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-energy-suppliers-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 3, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("Hourly"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.EnergyActivate,
                        ParentIdentification = null,
                        EnergySuppliers =
                        [
                            new()
                            {
                                Identification = new MeteringPointIdentification("two-energy-suppliers-please"),
                                EnergySupplier = "1111111111111",
                                StartDate = Instant.FromUtc(2021, 1, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 2, 1, 0, 0),
                            },
                            new()
                            {
                                Identification = new MeteringPointIdentification("two-energy-suppliers-please"),
                                EnergySupplier = "2222222222222",
                                StartDate = Instant.FromUtc(2021, 2, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 3, 1, 0, 0),
                            },
                        ],
                    },
                ]),
                "faulty-two-master-data-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("faulty-two-master-data-please"),
                        ValidFrom = Instant.FromUtc(2021, 1, 1, 0, 0),
                        ValidTo = Instant.FromUtc(2021, 3, 1, 0, 0),
                        GridAreaCode = new GridAreaCode("804"),
                        GridAccessProvider = "9999999999999",
                        ConnectionState = ConnectionState.Connected,
                        Type = MeteringPointType.Consumption,
                        SubType = MeteringPointSubType.Physical,
                        Resolution = new Resolution("Hourly"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.EnergyActivate,
                        ParentIdentification = null,
                        EnergySuppliers =
                        [
                            new()
                            {
                                Identification = new MeteringPointIdentification("faulty-two-master-data-please"),
                                EnergySupplier = "1111111111111",
                                StartDate = Instant.FromUtc(2021, 1, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 2, 1, 0, 0),
                            },
                            new()
                            {
                                Identification = new MeteringPointIdentification("faulty-two-master-data-please"),
                                EnergySupplier = "2222222222222",
                                StartDate = Instant.FromUtc(2021, 2, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 3, 1, 0, 0),
                            },
                        ],
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
                        Resolution = new Resolution("Hourly"),
                        Unit = MeasureUnit.kWh,
                        ProductId = ProductId.EnergyActivate,
                        ParentIdentification = null,
                        EnergySuppliers = [],
                    },
                ]),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(meteringPointIdentification),
                    meteringPointIdentification,
                    null),
            };
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
