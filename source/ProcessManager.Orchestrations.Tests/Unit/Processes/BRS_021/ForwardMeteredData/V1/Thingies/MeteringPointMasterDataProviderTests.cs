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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Thingies;
using FluentAssertions;
using NodaTime;
using PMMeteringPointMasterData =
    Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.
    MeteringPointMasterData;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.Thingies;

public class MeteringPointMasterDataProviderTests
{
    private readonly ElectricityMarketViewsMock _electricityMarketViews;
    private readonly MeteringPointMasterDataProvider _sut;

    public MeteringPointMasterDataProviderTests()
    {
        _electricityMarketViews = new ElectricityMarketViewsMock();
        _sut = new MeteringPointMasterDataProvider(_electricityMarketViews);
    }

    [Fact]
    public void METHOD()
    {
        /* TODO
            * Unpacking:
                - [T] No MD
                - [T] One MD, no ES
                - [T] One MD, one ES
                - [T] One MD, two ES
                - [T] Two MD, two ES
                - [T] Two MD, no ES + one ES
            * Sanity check
                (use one MD, two ES)
                - [ ] Hole in dates
                - [ ] Overlap in dates
                - [ ] ES dates not matching MD dates
                - [ ] Change to type
                - [ ] Change to measurement unit
                - [ ] change to product id
         */
    }

    [Fact]
    public async Task Given_NoMasterData_When_GetAndConvertMasterData_Then_Empty()
    {
        (await _sut.GetAndConvertMasterData("no-master-data-please", "2021-01-01T00:00:00Z", "2021-01-01T00:00:00Z"))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Given_MasterDataWithNoEnergySuppliers_When_GetAndConvertMasterData_Then_Error()
    {
        var act = async () => await _sut.GetAndConvertMasterData(
            "no-energy-suppliers-please",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should().ThrowAsync<Exception>().WithMessage("Metering point master data is not consistent");
    }

    [Fact]
    public async Task Given_MasterDataWithOneEnergySupplier_When_GetAndConvertMasterData_Then_Single()
    {
        var meteringPointMasterData = await _sut.GetAndConvertMasterData(
            "one-energy-supplier-please",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        meteringPointMasterData
            .Should()
            .ContainSingle();

        var singleMasterData = meteringPointMasterData.Single();

        singleMasterData.MeteringPointId.Value.Should().Be("one-energy-supplier-please");
        singleMasterData.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
        singleMasterData.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
        singleMasterData.EnergySupplier.Value.Should().Be("1111111111111");
    }

    [Fact]
    public async Task Given_MasterDataWithTwoEnergySuppliers_When_GetAndConvertMasterData_Then_ListOfTwo()
    {
        var meteringPointMasterData = await _sut.GetAndConvertMasterData(
            "two-energy-suppliers-please",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

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
    public async Task Given_TwoMasterDataWithTwoEnergySuppliers_When_GetAndConvertMasterData_Then_ListOfFour()
    {
        var meteringPointMasterData = await _sut.GetAndConvertMasterData(
            "two-master-data-please",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        meteringPointMasterData
            .Should()
            .HaveCount(4);

        meteringPointMasterData.Should()
            .SatisfyRespectively(
                first =>
                {
                    first.MeteringPointId.Value.Should().Be("two-master-data-please");
                    first.ValidFrom.Should().Be(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
                    first.ValidTo.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    first.EnergySupplier.Value.Should().Be("1111111111111");
                },
                second =>
                {
                    second.MeteringPointId.Value.Should().Be("two-master-data-please");
                    second.ValidFrom.Should().Be(new DateTimeOffset(2021, 2, 1, 0, 0, 0, TimeSpan.Zero));
                    second.ValidTo.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    second.EnergySupplier.Value.Should().Be("2222222222222");
                },
                third =>
                {
                    third.MeteringPointId.Value.Should().Be("two-master-data-please");
                    third.ValidFrom.Should().Be(new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
                    third.ValidTo.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    third.EnergySupplier.Value.Should().Be("3333333333333");
                },
                fourth =>
                {
                    fourth.MeteringPointId.Value.Should().Be("two-master-data-please");
                    fourth.ValidFrom.Should().Be(new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.ValidTo.Should().Be(new DateTimeOffset(2021, 5, 1, 0, 0, 0, TimeSpan.Zero));
                    fourth.EnergySupplier.Value.Should().Be("4444444444444");
                });
    }

    [Fact]
    public async Task Given_TwoMasterDataWithOneFaultyEnergySuppliers_When_GetAndConvertMasterData_Then_Error()
    {
        var act = async () => await _sut.GetAndConvertMasterData(
            "faulty-two-master-data-please",
            "2021-01-01T00:00:00Z",
            "2021-01-01T00:00:00Z");

        await act.Should().ThrowAsync<Exception>().WithMessage("Metering point master data is not consistent");
    }

    private class ElectricityMarketViewsMock : IElectricityMarketViews
    {
        public Task<IEnumerable<MeteringPointMasterData>> GetMeteringPointMasterDataChangesAsync(
            MeteringPointIdentification meteringPointIdentification,
            Interval interval)
        {
            return meteringPointIdentification.Value switch
            {
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
                        ParentIdentification = new MeteringPointIdentification("parent-identification"),
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
                        ParentIdentification = new MeteringPointIdentification("parent-identification"),
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
                        ParentIdentification = new MeteringPointIdentification("parent-identification"),
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
                "two-master-data-please" => Task.FromResult<IEnumerable<MeteringPointMasterData>>(
                [
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-master-data-please"),
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
                        ParentIdentification = new MeteringPointIdentification("parent-identification"),
                        EnergySuppliers =
                        [
                            new()
                            {
                                Identification = new MeteringPointIdentification("two-master-data-please"),
                                EnergySupplier = "1111111111111",
                                StartDate = Instant.FromUtc(2021, 1, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 2, 1, 0, 0),
                            },
                            new()
                            {
                                Identification = new MeteringPointIdentification("two-master-data-please"),
                                EnergySupplier = "2222222222222",
                                StartDate = Instant.FromUtc(2021, 2, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 3, 1, 0, 0),
                            },
                        ],
                    },
                    new()
                    {
                        Identification = new MeteringPointIdentification("two-master-data-please"),
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
                        ParentIdentification = new MeteringPointIdentification("parent-identification"),
                        EnergySuppliers =
                        [
                            new()
                            {
                                Identification = new MeteringPointIdentification("two-master-data-please"),
                                EnergySupplier = "3333333333333",
                                StartDate = Instant.FromUtc(2021, 3, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 4, 1, 0, 0),
                            },
                            new()
                            {
                                Identification = new MeteringPointIdentification("two-master-data-please"),
                                EnergySupplier = "4444444444444",
                                StartDate = Instant.FromUtc(2021, 4, 1, 0, 0),
                                EndDate = Instant.FromUtc(2021, 5, 1, 0, 0),
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
                        ParentIdentification = new MeteringPointIdentification("parent-identification"),
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
                        ParentIdentification = new MeteringPointIdentification("parent-identification"),
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
