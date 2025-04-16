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
using System.Globalization;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1;
using FluentAssertions;
using FluentAssertions.Execution;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.Shared.
    ElectricityMarket;

[SuppressMessage(
    "StyleCop.CSharp.ReadabilityRules",
    "SA1118:Parameter should not span multiple lines",
    Justification = "Readability")]
public class MeteringPointReceiversProviderTests
{
    private static readonly MeteringPointType _defaultMeteringPointType = MeteringPointType.Consumption;
    private static readonly Instant _defaultFrom = Instant.FromUtc(2025, 02, 28, 23, 00);
    private static readonly Instant _defaultTo = _defaultFrom.Plus(Duration.FromHours(1));
    private static readonly Resolution _defaultResolution = Resolution.QuarterHourly;
    private static readonly ActorNumber _defaultGridAccessProvider = ActorNumber.Create("1111111111111");
    private static readonly ActorNumber _defaultEnergySupplier = ActorNumber.Create("2222222222222");
    private static readonly ActorNumber _defaultParentEnergySupplier = ActorNumber.Create("9999999999999");
    private static readonly ActorNumber _defaultGridAccessProviderNeighbor1 = ActorNumber.Create("3333333333333");
    private static readonly ActorNumber _defaultGridAccessProviderNeighbor2 = ActorNumber.Create("4444444444444");

    private readonly MeteringPointReceiversProvider _sut = new(DateTimeZone.Utc);

    public static TheoryData<MeteringPointType> ChildMeteringPointTypes => new()
    {
        MeteringPointType.NetProduction,
        MeteringPointType.SupplyToGrid,
        MeteringPointType.ConsumptionFromGrid,
        MeteringPointType.WholesaleServicesInformation,
        MeteringPointType.OwnProduction,
        MeteringPointType.NetFromGrid,
        MeteringPointType.NetToGrid,
        MeteringPointType.TotalConsumption,
        MeteringPointType.Analysis,
        MeteringPointType.NotUsed,
        MeteringPointType.SurplusProductionGroup6,
        MeteringPointType.NetLossCorrection,
        MeteringPointType.OtherConsumption,
        MeteringPointType.OtherProduction,
        MeteringPointType.ExchangeReactiveEnergy,
        MeteringPointType.CollectiveNetProduction,
        MeteringPointType.CollectiveNetConsumption,
    };

    public static TheoryData<Resolution> GetAllResolutionsExceptMonthlyAndOther() => new(
        EnumerationRecordType.GetAll<Resolution>()
            .Where(r => r != Resolution.Monthly && r != Resolution.Other));

    [Fact]
    public void Given_MeteringPointTypeConsumption_When_GetReceivers_Then_ReceiversAreEnergySupplierAndDanishEnergyAgency()
    {
        var masterData = CreateMasterData(MeteringPointType.Consumption);

        var forwardMeteredDataInput = CreateForwardMeteredDataInput([masterData]);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData],
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Actors
            .Should()
            .HaveCount(2)
            .And.SatisfyRespectively(
                a =>
                {
                    a.ActorNumber.Should().Be(_defaultEnergySupplier);
                    a.ActorRole.Should().Be(ActorRole.EnergySupplier);
                },
                a =>
                {
                    a.ActorNumber.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    a.ActorRole.Should().Be(ActorRole.DanishEnergyAgency);
                });
    }

    [Fact]
    public void Given_MeteringPointTypeProduction_When_GetReceivers_Then_ReceiversAreEnergySupplierAndDanishEnergyAgency()
    {
        var masterData = CreateMasterData(MeteringPointType.Production);

        var forwardMeteredDataInput = CreateForwardMeteredDataInput([masterData]);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData],
                forwardMeteredDataInput));

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Actors
            .Should()
            .HaveCount(2)
            .And.SatisfyRespectively(
                a =>
                {
                    a.ActorNumber.Should().Be(_defaultEnergySupplier);
                    a.ActorRole.Should().Be(ActorRole.EnergySupplier);
                },
                a =>
                {
                    a.ActorNumber.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    a.ActorRole.Should().Be(ActorRole.DanishEnergyAgency);
                });
    }

    [Fact]
    public void Given_MeteringPointTypeExchange_When_GetReceivers_Then_ReceiversAreGridAccessProviderNeighbors()
    {
        var masterData = CreateMasterData(MeteringPointType.Exchange);

        var forwardMeteredDataInput = CreateForwardMeteredDataInput([masterData]);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData],
                forwardMeteredDataInput));

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Actors
            .Should()
            .HaveCount(2)
            .And.SatisfyRespectively(
                a =>
                {
                    a.ActorNumber.Should().Be(_defaultGridAccessProviderNeighbor1);
                    a.ActorRole.Should().Be(ActorRole.GridAccessProvider);
                },
                a =>
                {
                    a.ActorNumber.Should().Be(_defaultGridAccessProviderNeighbor2);
                    a.ActorRole.Should().Be(ActorRole.GridAccessProvider);
                });
    }

    [Fact]
    public void Given_MeteringPointTypeVeProduction_When_GetReceivers_Then_ReceiversAreSystemOperatorAndDanishEnergyAgency()
    {
        var masterData = CreateMasterData(MeteringPointType.VeProduction);

        var forwardMeteredDataInput = CreateForwardMeteredDataInput([masterData]);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData],
                forwardMeteredDataInput));

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Actors
            .Should()
            .HaveCount(3)
            .And.SatisfyRespectively(
                a =>
                {
                    a.ActorNumber.Value.Should().Be(DataHubDetails.SystemOperatorNumber);
                    a.ActorRole.Should().Be(ActorRole.SystemOperator);
                },
                a =>
                {
                    a.ActorNumber.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    a.ActorRole.Should().Be(ActorRole.DanishEnergyAgency);
                },
                a =>
                {
                    a.ActorNumber.Should().Be(_defaultEnergySupplier);
                    a.ActorRole.Should().Be(ActorRole.EnergySupplier);
                });
    }

    [Theory]
    [MemberData(nameof(GetAllResolutionsExceptMonthlyAndOther))]
    public void Given_SingleMasterDataPeriods_When_GetReceivers_Then_AllMeteredDataIsSentToTheSameReceivers(Resolution resolution)
    {
        var masterData1Start = Instant.FromUtc(2024, 02, 28, 23, 00);
        var masterData1End = masterData1Start.Plus(Duration.FromDays(42));

        List<MeteringPointMasterData> masterDataList =
        [
            CreateMasterData(
                from: masterData1Start,
                to: masterData1End,
                resolution: resolution),
        ];

        var forwardMeteredDataInput = CreateForwardMeteredDataInput(masterDataList);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                masterDataList,
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should()
            .ContainSingle()
            .And.SatisfyRespectively(
                r =>
                {
                    r.StartDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.StartDateTime));
                    r.EndDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.EndDateTime!));
                    r.Actors.Should()
                        .ContainSingle(a => a.ActorNumber == _defaultEnergySupplier);
                    r.MeteredData.Should().HaveSameCount(forwardMeteredDataInput.MeteredDataList);
                    r.MeteredData.First().Position.Should().Be(1);
                    r.MeteredData.Last().Position.Should().Be(r.MeteredData.Count);
                });
    }

    [Theory]
    [MemberData(nameof(GetAllResolutionsExceptMonthlyAndOther))]
    public void Given_MultipleMasterDataPeriods_When_GetReceivers_Then_MeteredDataIsSplitCorrectlyToReceivers(Resolution resolution)
    {
        var elementsPerDayForResolution = resolution switch
        {
            var r when r == Resolution.QuarterHourly => 24 * 4, // 15 minutes resolution = 24 * 4 = 96 elements per day.
            var r when r == Resolution.Hourly => 24, // 1 hour resolution = 24 elements per day.
            var r when r == Resolution.Daily => 1, // 1 day resolution = 1 elements per day.
            _ => throw new ArgumentOutOfRangeException(// TODO: Is monthly resolution supported for forward metered data?
                paramName: nameof(resolution),
                actualValue: resolution.Name,
                message: "Invalid resolution"),
        };

        const int masterData1Days = 80;
        var masterData1Start = Instant.FromUtc(2024, 02, 28, 23, 00);
        var masterData1End = masterData1Start.Plus(Duration.FromDays(masterData1Days));
        var masterData1Receiver = ActorNumber.Create("0000000000001");

        const int masterData2Days = 17;
        var masterData2Start = masterData1End;
        var masterData2End = masterData2Start.Plus(Duration.FromDays(masterData2Days));
        var masterData2Receiver = ActorNumber.Create("0000000000002");

        const int masterData3Days = 268;
        var masterData3Start = masterData2End;
        var masterData3End = masterData3Start.Plus(Duration.FromDays(masterData3Days)); // The total period is 365 days.
        var masterData3Receiver = ActorNumber.Create("0000000000003");

        var masterData1 = CreateMasterData(
            from: masterData1Start,
            to: masterData1End,
            resolution: resolution,
            energySupplier: masterData1Receiver);
        var masterData2 = CreateMasterData(
            from: masterData2Start,
            to: masterData2End,
            resolution: resolution,
            energySupplier: masterData2Receiver);
        var masterData3 = CreateMasterData(
            from: masterData3Start,
            to: masterData3End,
            resolution: resolution,
            energySupplier: masterData3Receiver);

        List<MeteringPointMasterData> masterDataList = [masterData1, masterData2, masterData3];

        var forwardMeteredDataInput = CreateForwardMeteredDataInput(masterDataList);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                masterDataList,
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should()
            .HaveCount(3)
            .And.SatisfyRespectively(
                r =>
                {
                    r.StartDateTime.Should().Be(masterData1Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData1End.ToDateTimeOffset());
                    r.Actors.Should()
                        .ContainSingle(a => a.ActorNumber == masterData1Receiver)
                        .And.NotContain(a => a.ActorNumber == masterData2Receiver || a.ActorNumber == masterData3Receiver);
                    r.MeteredData.Should().HaveCount(masterData1Days * elementsPerDayForResolution);
                    r.MeteredData.First().Position.Should().Be(1);
                    r.MeteredData.Last().Position.Should().Be(r.MeteredData.Count);
                },
                r =>
                {
                    r.StartDateTime.Should().Be(masterData2Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData2End.ToDateTimeOffset());
                    r.Actors.Should()
                        .ContainSingle(a => a.ActorNumber == masterData2Receiver)
                        .And.NotContain(a => a.ActorNumber == masterData1Receiver || a.ActorNumber == masterData3Receiver);
                    r.MeteredData.Should().HaveCount(masterData2Days * elementsPerDayForResolution);
                    r.MeteredData.First().Position.Should().Be(1);
                    r.MeteredData.Last().Position.Should().Be(r.MeteredData.Count);
                },
                r =>
                {
                    r.StartDateTime.Should().Be(masterData3Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData3End.ToDateTimeOffset());
                    r.Actors.Should()
                        .ContainSingle(a => a.ActorNumber == masterData3Receiver)
                        .And.NotContain(a => a.ActorNumber == masterData1Receiver || a.ActorNumber == masterData2Receiver);
                    r.MeteredData.Should().HaveCount(masterData3Days * elementsPerDayForResolution);
                    r.MeteredData.First().Position.Should().Be(1);
                    r.MeteredData.Last().Position.Should().Be(r.MeteredData.Count);
                });
    }

    [Fact(Skip = "Different resolutions in the same transaction period is not supported (should be rejected by business validation)")]
    public void Given_MultipleMasterDataPeriodsWithDifferentResolutions_When_GetReceivers_Then_MeteredDataIsSplitToCorrectMasterDataPeriods()
    {
        const int masterData1Days = 80;
        var masterData1Resolution = Resolution.Hourly;
        const int masterData1ElementsPerDay = 24; // Hourly resolution = 24 elements per day.
        var masterData1Start = Instant.FromUtc(2024, 02, 28, 23, 00);
        var masterData1End = masterData1Start.Plus(Duration.FromDays(masterData1Days));

        const int masterData2Days = 17;
        var masterData2Resolution = Resolution.Daily;
        const int masterData2ElementsPerDay = 1; // Daily resolution = 1 element per day.
        var masterData2Start = masterData1End;
        var masterData2End = masterData2Start.Plus(Duration.FromDays(masterData2Days));

        const int masterData3Days = 268;
        var masterData3Resolution = Resolution.QuarterHourly;
        const int masterData3ElementsPerDay = 24 * 4; // 15 minutes resolution = 24 * 4 = 96 elements per day.
        var masterData3Start = masterData2End;
        var masterData3End = masterData3Start.Plus(Duration.FromDays(masterData3Days));

        var masterData1 = CreateMasterData(from: masterData1Start, to: masterData1End, resolution: masterData1Resolution);
        var masterData2 = CreateMasterData(from: masterData2Start, to: masterData2End, resolution: masterData2Resolution);
        var masterData3 = CreateMasterData(from: masterData3Start, to: masterData3End, resolution: masterData3Resolution);

        List<MeteringPointMasterData> masterDataList = [masterData1, masterData2, masterData3];

        var forwardMeteredDataInput = CreateForwardMeteredDataInput(masterDataList);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                masterDataList,
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should()
            .HaveCount(3)
            .And.SatisfyRespectively(
                (r) =>
                {
                    r.StartDateTime.Should().Be(masterData1Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData1End.ToDateTimeOffset());
                    r.MeteredData.Should().HaveCount(masterData1Days * masterData1ElementsPerDay);
                    r.MeteredData.First().Position.Should().Be(1);
                    r.MeteredData.Last().Position.Should().Be(r.MeteredData.Count);
                },
                (r) =>
                {
                    r.StartDateTime.Should().Be(masterData2Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData2End.ToDateTimeOffset());
                    r.MeteredData.Should().HaveCount(masterData2Days * masterData2ElementsPerDay);
                    r.MeteredData.First().Position.Should().Be(1);
                    r.MeteredData.Last().Position.Should().Be(r.MeteredData.Count);
                },
                (r) =>
                {
                    r.StartDateTime.Should().Be(masterData3Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData3End.ToDateTimeOffset());
                    r.MeteredData.Should().HaveCount(masterData3Days * masterData3ElementsPerDay);
                    r.MeteredData.First().Position.Should().Be(1);
                    r.MeteredData.Last().Position.Should().Be(r.MeteredData.Count);
                });
    }

    [Theory]
    [MemberData(nameof(ChildMeteringPointTypes))]
    public void Given_ChildMeteringPointType_When_GetReceivers_Then_ReceiversAreTheParentEnergySuppliers(
        MeteringPointType meteringPointType)
    {
        var masterData = CreateMasterData(meteringPointType, parentMeteringPointId: "parent-metering-point-id");

        var forwardMeteredDataInput = CreateForwardMeteredDataInput([masterData]);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData],
                forwardMeteredDataInput));

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Actors.Should()
            .ContainSingle()
            .And.SatisfyRespectively(
                mar =>
                {
                    mar.ActorNumber.Should().Be(_defaultParentEnergySupplier);
                    mar.ActorRole.Should().Be(ActorRole.EnergySupplier);
                });
    }

    [Fact]
    public void
        Given_OneMasterDataPeriodWhichExceedsStartAndEndPeriodOfInput_When_GetReceivers_Then_OnePackageWithBoundedStartAndEndDates()
    {
        var masterData = CreateMasterData(
            MeteringPointType.Consumption,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T23:00:00Z")
            .WithEndDateTime("2025-03-01T00:00:00Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 649)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData],
                forwardMeteredDataInput));

        var package = receiversWithMeteredData.Should().ContainSingle().Subject;
        package.StartDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.StartDateTime));
        package.EndDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.EndDateTime!));
        package.MeteredData.Should().HaveCount(649);
    }

    [Fact]
    public void
        Given_OneMasterDataPeriodWhichExceedsStartPeriodOfInput_When_GetReceivers_Then_OnePackageWithBoundedStartDate()
    {
        var masterData = CreateMasterData(
            MeteringPointType.Consumption,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T23:00:00Z")
            .WithEndDateTime("2025-03-01T00:00:00Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 649)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData],
                forwardMeteredDataInput));

        var package = receiversWithMeteredData.Should().ContainSingle().Subject;
        package.StartDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.StartDateTime));
        package.EndDateTime.Should().Be(masterData.ValidTo);
        package.MeteredData.Should().HaveCount(649);
    }

    [Fact]
    public void
        Given_OneMasterDataPeriodWhichExceedsEndPeriodOfInput_When_GetReceivers_Then_OnePackageWithBoundedEndDate()
    {
        var masterData = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 2, 1, 23, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T23:00:00Z")
            .WithEndDateTime("2025-03-01T00:00:00Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 649)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData],
                forwardMeteredDataInput));

        var package = receiversWithMeteredData.Should().ContainSingle().Subject;
        package.StartDateTime.Should().Be(masterData.ValidFrom);
        package.EndDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.EndDateTime!));
        package.MeteredData.Should().HaveCount(649);
    }

    [Fact]
    public void
        Given_TwoMasterDataPeriodsWhichExceedsStartAndEndPeriodOfInput_When_GetReceivers_Then_TwoPackagesWithBoundedStartAndEndDates()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T00:00:00Z")
            .WithEndDateTime("2025-04-01T00:00:00Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 59)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData1, masterData2],
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(2);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.StartDateTime));
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeteredData.Should().HaveCount(28);

        var second = receiversWithMeteredData.Last();
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.EndDateTime!));
        second.MeteredData.Should().HaveCount(31);
    }

    [Fact]
    public void
        Given_TwoMasterDataPeriodsWhichExceedsStartPeriodOfInput_When_GetReceivers_Then_TwoPackagesWithBoundedStartDate()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T00:00:00Z")
            .WithEndDateTime("2025-04-01T00:00:00Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 59)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData1, masterData2],
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(2);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.StartDateTime));
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeteredData.Should().HaveCount(28);

        var second = receiversWithMeteredData.Last();
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeteredData.Should().HaveCount(31);
    }

    [Fact]
    public void
        Given_TwoMasterDataPeriodsWhichExceedsEndPeriodOfInput_When_GetReceivers_Then_TwoPackagesWithBoundedEndDate()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T00:00:00Z")
            .WithEndDateTime("2025-04-01T00:00:00Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 59)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData1, masterData2],
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(2);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(masterData1.ValidFrom);
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeteredData.Should().HaveCount(28);

        var second = receiversWithMeteredData.Last();
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.EndDateTime!));
        second.MeteredData.Should().HaveCount(31);
    }

    [Fact]
    public void
        Given_ThreeMasterDataPeriodsWhichExceedsStartAndEndPeriodOfInput_When_GetReceivers_Then_ThreePackagesWithBoundedStartAndEndDates()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData3 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T00:00:00Z")
            .WithEndDateTime("2025-05-01T00:00:00Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 89)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData1, masterData2, masterData3],
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(3);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.StartDateTime));
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeteredData.Should().HaveCount(28);

        var second = receiversWithMeteredData[1];
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeteredData.Should().HaveCount(31);

        var third = receiversWithMeteredData.Last();
        third.StartDateTime.Should().Be(masterData3.ValidFrom);
        third.EndDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.EndDateTime!));
        third.MeteredData.Should().HaveCount(30);
    }

    [Fact]
    public void
        Given_ThreeMasterDataPeriodsWhichExceedsStartPeriodOfInput_When_GetReceivers_Then_ThreePackagesWithBoundedStartDate()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData3 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T00:00:00Z")
            .WithEndDateTime("2025-05-01T00:00:00Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 89)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData1, masterData2, masterData3],
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(3);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.StartDateTime));
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeteredData.Should().HaveCount(28);

        var second = receiversWithMeteredData[1];
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeteredData.Should().HaveCount(31);

        var third = receiversWithMeteredData.Last();
        third.StartDateTime.Should().Be(masterData3.ValidFrom);
        third.EndDateTime.Should().Be(masterData3.ValidTo);
        third.MeteredData.Should().HaveCount(30);
    }

    [Fact]
    public void
        Given_ThreeMasterDataPeriodsWhichExceedsEndPeriodOfInput_When_GetReceivers_Then_ThreePackagesWithBoundedEndDate()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData3 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T00:00:00Z")
            .WithEndDateTime("2025-05-01T00:00:00Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 89)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData1, masterData2, masterData3],
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(3);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(masterData1.ValidFrom);
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeteredData.Should().HaveCount(28);

        var second = receiversWithMeteredData[1];
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeteredData.Should().HaveCount(31);

        var third = receiversWithMeteredData.Last();
        third.StartDateTime.Should().Be(masterData3.ValidFrom);
        third.EndDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.EndDateTime!));
        third.MeteredData.Should().HaveCount(30);
    }

    [Fact(Skip = "This test is valid, but the feature is not implemented yet")]
    public void
        Given_MasterDataPeriodsWhichExceedsStartAndEndPeriodOfInputAndHolesPresent_When_GetReceivers_Then_ThreePackagesWithBoundedStartAndEndDatesAndHolesPreserved()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData3 = CreateMasterData(
            MeteringPointType.Consumption,
            from: new DateTimeOffset(2025, 4, 15, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var forwardMeteredDataInput = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-01T00:00:00Z")
            .WithEndDateTime("2025-05-01T00:00:00Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 89)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            EnergyQuantity: "1.4",
                            QuantityQuality: Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(
                [masterData1, masterData2, masterData3],
                forwardMeteredDataInput));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(3);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.StartDateTime));
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeteredData.Should().HaveCount(28);

        var second = receiversWithMeteredData[1];
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeteredData.Should().HaveCount(31);

        var third = receiversWithMeteredData.Last();
        third.StartDateTime.Should().Be(masterData3.ValidFrom);
        third.EndDateTime.Should().Be(DateTimeOffset.Parse(forwardMeteredDataInput.EndDateTime!));
        third.MeteredData.Should().HaveCount(30);
    }

    private MeteringPointMasterData CreateMasterData(
        MeteringPointType? meteringPointType = null,
        Instant? from = null,
        Instant? to = null,
        Resolution? resolution = null,
        ActorNumber? gridAccessProvider = null,
        ActorNumber? energySupplier = null,
        string? parentMeteringPointId = null)
    {
        return new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId("1"),
            ValidFrom: (from ?? _defaultFrom).ToDateTimeOffset(),
            ValidTo: (to ?? _defaultTo).ToDateTimeOffset(),
            GridAreaCode: new GridAreaCode("1"),
            GridAccessProvider: gridAccessProvider ?? _defaultGridAccessProvider,
            NeighborGridAreaOwners: [_defaultGridAccessProviderNeighbor1.Value, _defaultGridAccessProviderNeighbor2.Value],
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: meteringPointType ?? _defaultMeteringPointType,
            MeteringPointSubType: MeteringPointSubType.Physical,
            Resolution: resolution ?? _defaultResolution,
            MeasurementUnit: MeasurementUnit.KilowattHour,
            ProductId: "1",
            ParentMeteringPointId: parentMeteringPointId is not null
                ? new MeteringPointId(parentMeteringPointId)
                : null,
            EnergySupplier: parentMeteringPointId is not null
                ? _defaultParentEnergySupplier
                : energySupplier ?? _defaultEnergySupplier);
    }

    private ForwardMeteredDataInputV1 CreateForwardMeteredDataInput(
        IReadOnlyCollection<MeteringPointMasterData> masterData)
    {
        var currentPosition = 1;
        var meteredData = masterData
            .OrderBy(mpmd => mpmd.ValidFrom)
            .SelectMany(
                mpmd =>
                {
                    var resolutionAsDuration = mpmd.Resolution switch
                    {
                        var r when r == Resolution.QuarterHourly => Duration.FromMinutes(15),
                        var r when r == Resolution.Hourly => Duration.FromHours(1),
                        var r when r == Resolution.Daily => Duration.FromDays(1),
                        _ => throw new ArgumentOutOfRangeException(
                            paramName: nameof(mpmd.Resolution),
                            actualValue: mpmd.Resolution.Name,
                            message: "Invalid resolution"),
                    };

                    var currentTimestamp = mpmd.ValidFrom.ToInstant();
                    var meteredDataForMasterData = new List<ForwardMeteredDataInputV1.MeteredData>();
                    while (currentTimestamp < mpmd.ValidTo.ToInstant())
                    {
                        meteredDataForMasterData.Add(
                            new ForwardMeteredDataInputV1.MeteredData(
                                Position: currentPosition.ToString(),
                                EnergyQuantity: "1.4",
                                QuantityQuality: Quality.AsProvided.Name));

                        currentTimestamp = currentTimestamp.Plus(resolutionAsDuration);

                        currentPosition++;
                    }

                    return meteredDataForMasterData;
                })
            .ToList();

        var from = InstantPattern.General.Format(masterData.First().ValidFrom.ToInstant());
        var to = InstantPattern.General.Format(masterData.Last().ValidTo.ToInstant());

        return new ForwardMeteredDataInputV1(
            ActorMessageId: "1",
            TransactionId: "2",
            ActorNumber: "1234567890123",
            ActorRole: ActorRole.GridAccessProvider.Name,
            BusinessReason: BusinessReason.PeriodicMetering.Name,
            MeteringPointId: "1234567890123",
            MeteringPointType: masterData.First().MeteringPointType.Name,
            ProductNumber: "3",
            MeasureUnit: MeasurementUnit.KilowattHour.Name,
            RegistrationDateTime: from,
            Resolution: masterData.First().Resolution.Name,
            StartDateTime: from,
            EndDateTime: to,
            GridAccessProviderNumber: masterData.First().GridAccessProvider.Value,
            MeteredDataList: meteredData);
    }

    private MeteringPointReceiversProvider.FindReceiversInput CreateFindReceiversInput(
        IReadOnlyCollection<MeteringPointMasterData> masterData,
        ForwardMeteredDataInputV1 forwardMeteredDataInput)
    {
        var measureData = forwardMeteredDataInput.MeteredDataList
            .Select(
                md =>
                {
                    var position = int.Parse(md.Position!);

                    var energyQuantity = decimal.Parse(
                        md.EnergyQuantity!,
                        NumberFormatInfo.InvariantInfo);

                    // The input is already validated, so converting these should not fail.
                    return new ReceiversWithMeteredDataV1.AcceptedMeteredData(
                        Position: position,
                        EnergyQuantity: energyQuantity,
                        QuantityQuality: Quality.FromName(md.QuantityQuality!));
                })
            .ToList();

        return new MeteringPointReceiversProvider.FindReceiversInput(
            forwardMeteredDataInput.MeteringPointId!,
            InstantPatternWithOptionalSeconds.Parse(forwardMeteredDataInput.StartDateTime!).Value,
            InstantPatternWithOptionalSeconds.Parse(forwardMeteredDataInput.EndDateTime!).Value,
            masterData.First().Resolution,
            masterData,
            measureData);
    }
}
