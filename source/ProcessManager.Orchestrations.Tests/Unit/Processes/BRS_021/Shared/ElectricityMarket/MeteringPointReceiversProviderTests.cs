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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using FluentAssertions;
using FluentAssertions.Execution;
using NodaTime;
using NodaTime.Extensions;

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

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput([masterData]));

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Receivers
            .Should()
            .HaveCount(2)
            .And.SatisfyRespectively(
                a =>
                {
                    a.Number.Should().Be(_defaultEnergySupplier);
                    a.Role.Should().Be(ActorRole.EnergySupplier);
                },
                a =>
                {
                    a.Number.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    a.Role.Should().Be(ActorRole.DanishEnergyAgency);
                });
    }

    [Fact]
    public void Given_MeteringPointTypeProduction_When_GetReceivers_Then_ReceiversAreEnergySupplierAndDanishEnergyAgency()
    {
        var masterData = CreateMasterData(MeteringPointType.Production);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput([masterData]));

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Receivers
            .Should()
            .HaveCount(2)
            .And.SatisfyRespectively(
                a =>
                {
                    a.Number.Should().Be(_defaultEnergySupplier);
                    a.Role.Should().Be(ActorRole.EnergySupplier);
                },
                a =>
                {
                    a.Number.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    a.Role.Should().Be(ActorRole.DanishEnergyAgency);
                });
    }

    [Theory]
    [InlineData("Consumption")]
    [InlineData("Production")]
    public void Given_MeteringPointType_When_GetReceivers_Then_PeriodsAreCorrectForEnergySupplierAndDanishEnergyAgency(string mp)
    {
        var meteringPointType = MeteringPointType.FromName(mp);
        var firstPeriodWithEnergySupplier = new Interval(
            Instant.FromUtc(year: 2025, monthOfYear: 1, dayOfMonth: 1, hourOfDay: 23, minuteOfHour: 00),
            Instant.FromUtc(year: 2025, monthOfYear: 1, dayOfMonth: 2, hourOfDay: 23, minuteOfHour: 00));
        var periodWithoutEnergySupplier = new Interval(
            Instant.FromUtc(year: 2025, monthOfYear: 1, dayOfMonth: 2, hourOfDay: 23, minuteOfHour: 00),
            Instant.FromUtc(year: 2025, monthOfYear: 1, dayOfMonth: 3, hourOfDay: 23, minuteOfHour: 00));
        var secondPeriodWithEnergySupplier = new Interval(
            Instant.FromUtc(year: 2025, monthOfYear: 1, dayOfMonth: 3, hourOfDay: 23, minuteOfHour: 00),
            Instant.FromUtc(year: 2025, monthOfYear: 1, dayOfMonth: 4, hourOfDay: 23, minuteOfHour: 00));

        var masterData = CreateMasterData(meteringPointType, firstPeriodWithEnergySupplier.Start, firstPeriodWithEnergySupplier.End);

        var masterData2 = CreateMasterDataWithoutParentOrEnergySupplier(periodWithoutEnergySupplier, meteringPointType);

        var masterData3 = CreateMasterData(meteringPointType, secondPeriodWithEnergySupplier.Start, secondPeriodWithEnergySupplier.End);

        IReadOnlyCollection<MeteringPointMasterData> meteringPointMasterData = [masterData, masterData2, masterData3];
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput(meteringPointMasterData));

        // Assert that the EnergySupplier only receives data for periods where it is the supplier
        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Where(x => x.Receivers.Contains(
            new Actor(_defaultEnergySupplier, ActorRole.EnergySupplier)))
            .Should()
            .HaveCount(2)
            .And
            .SatisfyRespectively(
            a =>
            {
                a.StartDateTime.Should().Be(firstPeriodWithEnergySupplier.Start.ToDateTimeOffset());
                a.EndDateTime.Should().Be(firstPeriodWithEnergySupplier.End.ToDateTimeOffset());
            },
            a =>
            {
                a.StartDateTime.Should().Be(secondPeriodWithEnergySupplier.Start.ToDateTimeOffset());
                a.EndDateTime.Should().Be(secondPeriodWithEnergySupplier.End.ToDateTimeOffset());
            });

        // Assert that the DanishEnergyAgency receives data for the full duration.
        receiversWithMeteredData.Where(x => x.Receivers.Contains(
            new Actor(
                ActorNumber.Create(DataHubDetails.DanishEnergyAgencyNumber),
                ActorRole.DanishEnergyAgency)))
            .Should()
            .HaveCount(meteringPointMasterData.Count)
            .And
            .SatisfyRespectively(
            a =>
            {
                a.StartDateTime.Should().Be(firstPeriodWithEnergySupplier.Start.ToDateTimeOffset());
                a.EndDateTime.Should().Be(firstPeriodWithEnergySupplier.End.ToDateTimeOffset());
            },
            a =>
            {
                a.StartDateTime.Should().Be(periodWithoutEnergySupplier.Start.ToDateTimeOffset());
                a.EndDateTime.Should().Be(periodWithoutEnergySupplier.End.ToDateTimeOffset());
            },
            a =>
            {
                a.StartDateTime.Should().Be(secondPeriodWithEnergySupplier.Start.ToDateTimeOffset());
                a.EndDateTime.Should().Be(secondPeriodWithEnergySupplier.End.ToDateTimeOffset());
            });
    }

    [Fact]
    public void Given_MeteringPointTypeExchange_When_GetReceivers_Then_ReceiversAreGridAccessProviderNeighbors()
    {
        var masterData = CreateMasterData(MeteringPointType.Exchange);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput([masterData]));

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Receivers
            .Should()
            .HaveCount(2)
            .And.SatisfyRespectively(
                a =>
                {
                    a.Number.Should().Be(_defaultGridAccessProviderNeighbor1);
                    a.Role.Should().Be(ActorRole.GridAccessProvider);
                },
                a =>
                {
                    a.Number.Should().Be(_defaultGridAccessProviderNeighbor2);
                    a.Role.Should().Be(ActorRole.GridAccessProvider);
                });
    }

    [Fact]
    public void Given_MeteringPointTypeVeProduction_When_GetReceivers_Then_ReceiversAreSystemOperatorAndDanishEnergyAgency()
    {
        var masterData = CreateMasterData(MeteringPointType.VeProduction);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            CreateFindReceiversInput([masterData]));

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Receivers
            .Should()
            .HaveCount(3)
            .And.SatisfyRespectively(
                a =>
                {
                    a.Number.Value.Should().Be(DataHubDetails.SystemOperatorNumber);
                    a.Role.Should().Be(ActorRole.SystemOperator);
                },
                a =>
                {
                    a.Number.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    a.Role.Should().Be(ActorRole.DanishEnergyAgency);
                },
                a =>
                {
                    a.Number.Should().Be(_defaultEnergySupplier);
                    a.Role.Should().Be(ActorRole.EnergySupplier);
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

        var findReceiversInput = CreateFindReceiversInput(masterDataList);
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should()
            .ContainSingle()
            .And.SatisfyRespectively(
                r =>
                {
                    r.StartDateTime.Should().Be(findReceiversInput.StartDateTime.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(findReceiversInput.EndDateTime.ToDateTimeOffset());
                    r.Receivers.Should()
                        .ContainSingle(a => a.Number == _defaultEnergySupplier);
                    r.MeasureDataList.Should().HaveSameCount(findReceiversInput.MeasureData);
                    r.MeasureDataList.First().Position.Should().Be(1);
                    r.MeasureDataList.Last().Position.Should().Be(r.MeasureDataList.Count);
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

        var findReceiversInput = CreateFindReceiversInput(masterDataList);
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should()
            .HaveCount(3)
            .And.SatisfyRespectively(
                r =>
                {
                    r.StartDateTime.Should().Be(masterData1Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData1End.ToDateTimeOffset());
                    r.Receivers.Should()
                        .ContainSingle(a => a.Number == masterData1Receiver)
                        .And.NotContain(a => a.Number == masterData2Receiver || a.Number == masterData3Receiver);
                    r.MeasureDataList.Should().HaveCount(masterData1Days * elementsPerDayForResolution);
                    r.MeasureDataList.First().Position.Should().Be(1);
                    r.MeasureDataList.Last().Position.Should().Be(r.MeasureDataList.Count);
                },
                r =>
                {
                    r.StartDateTime.Should().Be(masterData2Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData2End.ToDateTimeOffset());
                    r.Receivers.Should()
                        .ContainSingle(a => a.Number == masterData2Receiver)
                        .And.NotContain(a => a.Number == masterData1Receiver || a.Number == masterData3Receiver);
                    r.MeasureDataList.Should().HaveCount(masterData2Days * elementsPerDayForResolution);
                    r.MeasureDataList.First().Position.Should().Be(1);
                    r.MeasureDataList.Last().Position.Should().Be(r.MeasureDataList.Count);
                },
                r =>
                {
                    r.StartDateTime.Should().Be(masterData3Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData3End.ToDateTimeOffset());
                    r.Receivers.Should()
                        .ContainSingle(a => a.Number == masterData3Receiver)
                        .And.NotContain(a => a.Number == masterData1Receiver || a.Number == masterData2Receiver);
                    r.MeasureDataList.Should().HaveCount(masterData3Days * elementsPerDayForResolution);
                    r.MeasureDataList.First().Position.Should().Be(1);
                    r.MeasureDataList.Last().Position.Should().Be(r.MeasureDataList.Count);
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

        var findReceiversInput = CreateFindReceiversInput(masterDataList);
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should()
            .HaveCount(3)
            .And.SatisfyRespectively(
                (r) =>
                {
                    r.StartDateTime.Should().Be(masterData1Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData1End.ToDateTimeOffset());
                    r.MeasureDataList.Should().HaveCount(masterData1Days * masterData1ElementsPerDay);
                    r.MeasureDataList.First().Position.Should().Be(1);
                    r.MeasureDataList.Last().Position.Should().Be(r.MeasureDataList.Count);
                },
                (r) =>
                {
                    r.StartDateTime.Should().Be(masterData2Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData2End.ToDateTimeOffset());
                    r.MeasureDataList.Should().HaveCount(masterData2Days * masterData2ElementsPerDay);
                    r.MeasureDataList.First().Position.Should().Be(1);
                    r.MeasureDataList.Last().Position.Should().Be(r.MeasureDataList.Count);
                },
                (r) =>
                {
                    r.StartDateTime.Should().Be(masterData3Start.ToDateTimeOffset());
                    r.EndDateTime.Should().Be(masterData3End.ToDateTimeOffset());
                    r.MeasureDataList.Should().HaveCount(masterData3Days * masterData3ElementsPerDay);
                    r.MeasureDataList.First().Position.Should().Be(1);
                    r.MeasureDataList.Last().Position.Should().Be(r.MeasureDataList.Count);
                });
    }

    [Theory]
    [MemberData(nameof(ChildMeteringPointTypes))]
    public void Given_ChildMeteringPointType_When_GetReceivers_Then_ReceiversAreTheParentEnergySuppliers(
        MeteringPointType meteringPointType)
    {
        var masterData = CreateMasterData(meteringPointType, parentMeteringPointId: "parent-metering-point-id");

        var findReceiversInput = CreateFindReceiversInput([masterData]);
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Receivers.Should()
            .ContainSingle()
            .And.SatisfyRespectively(
                mar =>
                {
                    mar.Number.Should().Be(_defaultParentEnergySupplier);
                    mar.Role.Should().Be(ActorRole.EnergySupplier);
                });
    }

    [Fact]
    public void
        Given_OneMasterDataPeriodWhichExceedsStartAndEndPeriodOfInput_When_GetReceivers_Then_OnePackageWithBoundedStartAndEndDates()
    {
        var masterData = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Hourly,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData],
            startDateTime: Instant.FromUtc(2025, 02, 01, 23, 00),
            endDateTime: Instant.FromUtc(2025, 03, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        var package = receiversWithMeteredData.Should().ContainSingle().Subject;
        package.StartDateTime.Should().Be(findReceiversInput.StartDateTime.ToDateTimeOffset());
        package.EndDateTime.Should().Be(findReceiversInput.EndDateTime.ToDateTimeOffset());
        package.MeasureDataList.Should().HaveCount(649);
    }

    [Fact]
    public void
        Given_OneMasterDataPeriodWhichExceedsStartPeriodOfInput_When_GetReceivers_Then_OnePackageWithBoundedStartDate()
    {
        var masterData = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Hourly,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData],
            startDateTime: Instant.FromUtc(2025, 02, 01, 23, 00),
            endDateTime: Instant.FromUtc(2025, 03, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        var package = receiversWithMeteredData.Should().ContainSingle().Subject;
        package.StartDateTime.Should().Be(findReceiversInput.StartDateTime.ToDateTimeOffset());
        package.EndDateTime.Should().Be(masterData.ValidTo);
        package.MeasureDataList.Should().HaveCount(649);
    }

    [Fact]
    public void
        Given_OneMasterDataPeriodWhichExceedsEndPeriodOfInput_When_GetReceivers_Then_OnePackageWithBoundedEndDate()
    {
        var masterData = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Hourly,
            from: new DateTimeOffset(2025, 2, 1, 23, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData],
            startDateTime: Instant.FromUtc(2025, 02, 01, 23, 00),
            endDateTime: Instant.FromUtc(2025, 03, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        var package = receiversWithMeteredData.Should().ContainSingle().Subject;
        package.StartDateTime.Should().Be(masterData.ValidFrom);
        package.EndDateTime.Should().Be(findReceiversInput.EndDateTime.ToDateTimeOffset());
        package.MeasureDataList.Should().HaveCount(649);
    }

    [Fact]
    public void
        Given_TwoMasterDataPeriodsWhichExceedsStartAndEndPeriodOfInput_When_GetReceivers_Then_TwoPackagesWithBoundedStartAndEndDates()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData1, masterData2],
            startDateTime: Instant.FromUtc(2025, 02, 01, 00, 00),
            endDateTime: Instant.FromUtc(2025, 04, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(2);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(findReceiversInput.StartDateTime.ToDateTimeOffset());
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeasureDataList.Should().HaveCount(28);

        var second = receiversWithMeteredData.Last();
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(findReceiversInput.EndDateTime.ToDateTimeOffset());
        second.MeasureDataList.Should().HaveCount(31);
    }

    [Fact]
    public void
        Given_TwoMasterDataPeriodsWhichExceedsStartPeriodOfInput_When_GetReceivers_Then_TwoPackagesWithBoundedStartDate()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData1, masterData2],
            startDateTime: Instant.FromUtc(2025, 02, 01, 00, 00),
            endDateTime: Instant.FromUtc(2025, 04, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(2);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(findReceiversInput.StartDateTime.ToDateTimeOffset());
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeasureDataList.Should().HaveCount(28);

        var second = receiversWithMeteredData.Last();
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeasureDataList.Should().HaveCount(31);
    }

    [Fact]
    public void
        Given_TwoMasterDataPeriodsWhichExceedsEndPeriodOfInput_When_GetReceivers_Then_TwoPackagesWithBoundedEndDate()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData1, masterData2],
            startDateTime: Instant.FromUtc(2025, 02, 01, 00, 00),
            endDateTime: Instant.FromUtc(2025, 04, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(2);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(masterData1.ValidFrom);
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeasureDataList.Should().HaveCount(28);

        var second = receiversWithMeteredData.Last();
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(findReceiversInput.EndDateTime.ToDateTimeOffset());
        second.MeasureDataList.Should().HaveCount(31);
    }

    [Fact]
    public void
        Given_ThreeMasterDataPeriodsWhichExceedsStartAndEndPeriodOfInput_When_GetReceivers_Then_ThreePackagesWithBoundedStartAndEndDates()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData3 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData1, masterData2, masterData3],
            startDateTime: Instant.FromUtc(2025, 02, 01, 00, 00),
            endDateTime: Instant.FromUtc(2025, 05, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(3);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(findReceiversInput.StartDateTime.ToDateTimeOffset());
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeasureDataList.Should().HaveCount(28);

        var second = receiversWithMeteredData[1];
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeasureDataList.Should().HaveCount(31);

        var third = receiversWithMeteredData.Last();
        third.StartDateTime.Should().Be(masterData3.ValidFrom);
        third.EndDateTime.Should().Be(findReceiversInput.EndDateTime.ToDateTimeOffset());
        third.MeasureDataList.Should().HaveCount(30);
    }

    [Fact]
    public void
        Given_ThreeMasterDataPeriodsWhichExceedsStartPeriodOfInput_When_GetReceivers_Then_ThreePackagesWithBoundedStartDate()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData3 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData1, masterData2, masterData3],
            startDateTime: Instant.FromUtc(2025, 02, 01, 00, 00),
            endDateTime: Instant.FromUtc(2025, 05, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(3);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(findReceiversInput.StartDateTime.ToDateTimeOffset());
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeasureDataList.Should().HaveCount(28);

        var second = receiversWithMeteredData[1];
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeasureDataList.Should().HaveCount(31);

        var third = receiversWithMeteredData.Last();
        third.StartDateTime.Should().Be(masterData3.ValidFrom);
        third.EndDateTime.Should().Be(masterData3.ValidTo);
        third.MeasureDataList.Should().HaveCount(30);
    }

    [Fact]
    public void
        Given_ThreeMasterDataPeriodsWhichExceedsEndPeriodOfInput_When_GetReceivers_Then_ThreePackagesWithBoundedEndDate()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData3 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData1, masterData2, masterData3],
            startDateTime: Instant.FromUtc(2025, 02, 01, 00, 00),
            endDateTime: Instant.FromUtc(2025, 05, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(3);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(masterData1.ValidFrom);
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeasureDataList.Should().HaveCount(28);

        var second = receiversWithMeteredData[1];
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeasureDataList.Should().HaveCount(31);

        var third = receiversWithMeteredData.Last();
        third.StartDateTime.Should().Be(masterData3.ValidFrom);
        third.EndDateTime.Should().Be(findReceiversInput.EndDateTime.ToDateTimeOffset());
        third.MeasureDataList.Should().HaveCount(30);
    }

    [Fact(Skip = "This test is valid, but the feature is not implemented yet")]
    public void
        Given_MasterDataPeriodsWhichExceedsStartAndEndPeriodOfInputAndHolesPresent_When_GetReceivers_Then_ThreePackagesWithBoundedStartAndEndDatesAndHolesPreserved()
    {
        var masterData1 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: DateTimeOffset.MinValue.ToInstant(),
            to: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData2 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero).ToInstant());

        var masterData3 = CreateMasterData(
            MeteringPointType.Consumption,
            resolution: Resolution.Daily,
            from: new DateTimeOffset(2025, 4, 15, 0, 0, 0, TimeSpan.Zero).ToInstant(),
            to: DateTimeOffset.MaxValue.ToInstant());

        var findReceiversInput = CreateFindReceiversInput(
            [masterData1, masterData2, masterData3],
            startDateTime: Instant.FromUtc(2025, 02, 01, 00, 00),
            endDateTime: Instant.FromUtc(2025, 05, 01, 00, 00));
        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            findReceiversInput);

        using var assertionScope = new AssertionScope();
        receiversWithMeteredData.Should().HaveCount(3);
        var first = receiversWithMeteredData.First();
        first.StartDateTime.Should().Be(findReceiversInput.StartDateTime.ToDateTimeOffset());
        first.EndDateTime.Should().Be(masterData1.ValidTo);
        first.MeasureDataList.Should().HaveCount(28);

        var second = receiversWithMeteredData[1];
        second.StartDateTime.Should().Be(masterData2.ValidFrom);
        second.EndDateTime.Should().Be(masterData2.ValidTo);
        second.MeasureDataList.Should().HaveCount(31);

        var third = receiversWithMeteredData.Last();
        third.StartDateTime.Should().Be(masterData3.ValidFrom);
        third.EndDateTime.Should().Be(findReceiversInput.EndDateTime.ToDateTimeOffset());
        third.MeasureDataList.Should().HaveCount(30);
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

    private MeteringPointReceiversProvider.FindReceiversInput CreateFindReceiversInput(
        IReadOnlyCollection<MeteringPointMasterData> masterData,
        Resolution? resolution = null,
        Instant? startDateTime = null,
        Instant? endDateTime = null)
    {
        var currentPosition = 1;
        var meteredData = masterData
            .OrderBy(mpmd => mpmd.ValidFrom)
            .SelectMany(
                mpmd =>
                {
                    var resolutionAsDuration = (resolution ?? mpmd.Resolution) switch
                    {
                        var r when r == Resolution.QuarterHourly => Duration.FromMinutes(15),
                        var r when r == Resolution.Hourly => Duration.FromHours(1),
                        var r when r == Resolution.Daily => Duration.FromDays(1),
                        _ => throw new ArgumentOutOfRangeException(
                            paramName: nameof(mpmd.Resolution),
                            actualValue: mpmd.Resolution.Name,
                            message: "Invalid resolution"),
                    };

                    var boundedValidFrom = Instant.Max(startDateTime ?? Instant.MinValue, mpmd.ValidFrom.ToInstant());
                    var boundedValidTo = Instant.Min(endDateTime ?? Instant.MaxValue, mpmd.ValidTo.ToInstant());

                    var currentTimestamp = boundedValidFrom;
                    var meteredDataForMasterData = new List<ReceiversWithMeasureData.MeasureData>();
                    while (currentTimestamp < boundedValidTo)
                    {
                        meteredDataForMasterData.Add(
                            new ReceiversWithMeasureData.MeasureData(
                                Position: currentPosition,
                                EnergyQuantity: 1.4m,
                                QuantityQuality: Quality.AsProvided));

                        currentTimestamp = currentTimestamp.Plus(resolutionAsDuration);

                        currentPosition++;
                    }

                    return meteredDataForMasterData;
                })
            .ToList();

        return new MeteringPointReceiversProvider.FindReceiversInput(
            MeteringPointId: "1234567890123",
            StartDateTime: startDateTime ?? masterData.First().ValidFrom.ToInstant(),
            EndDateTime: endDateTime ?? masterData.Last().ValidTo.ToInstant(),
            Resolution: masterData.First().Resolution,
            MasterData: masterData,
            MeasureData: meteredData);
    }

    private MeteringPointMasterData CreateMasterDataWithoutParentOrEnergySupplier(Interval period, MeteringPointType? mp)
    {
        return new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId("1"),
            ValidFrom: period.Start.ToDateTimeOffset(),
            ValidTo: period.End.ToDateTimeOffset(),
            GridAreaCode: new GridAreaCode("1"),
            GridAccessProvider: _defaultGridAccessProvider,
            NeighborGridAreaOwners: [_defaultGridAccessProviderNeighbor1.Value, _defaultGridAccessProviderNeighbor2.Value],
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: mp ?? _defaultMeteringPointType,
            MeteringPointSubType: MeteringPointSubType.Physical,
            Resolution: _defaultResolution,
            MeasurementUnit: MeasurementUnit.KilowattHour,
            ProductId: "1",
            ParentMeteringPointId: null,
            EnergySupplier: null);
    }
}
