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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using FluentAssertions.Execution;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.
    ElectricityMarket;

public class MeteringPointReceiversProviderTests
{
    private static readonly MeteringPointType _defaultMeteringPointType = MeteringPointType.Consumption;
    private static readonly Instant _defaultFrom = Instant.FromUtc(2025, 02, 28, 23, 00);
    private static readonly Instant _defaultTo = _defaultFrom.Plus(Duration.FromHours(1));
    private static readonly Resolution _defaultResolution = Resolution.QuarterHourly;
    private static readonly ActorNumber _defaultGridAccessProvider = ActorNumber.Create("1111111111111");
    private static readonly ActorNumber _defaultEnergySupplier = ActorNumber.Create("2222222222222");
    private static readonly ActorNumber _defaultGridAccessProviderNeighbor1 = ActorNumber.Create("3333333333333");
    private static readonly ActorNumber _defaultGridAccessProviderNeighbor2 = ActorNumber.Create("4444444444444");

    private readonly MeteringPointReceiversProvider _sut = new(DateTimeZone.Utc);

    public static TheoryData<Resolution> GetAllResolutionsExceptMonthly() => new(
        EnumerationRecordType.GetAll<Resolution>()
            .Where(r => r != Resolution.Monthly));

    [Fact]
    public void Given_MeteringPointTypeConsumption_When_GetReceivers_Then_ReceiversAreEnergySupplierAndDanishEnergyAgency()
    {
        var masterData = CreateMasterData(MeteringPointType.Consumption);

        var forwardMeteredDataInput = CreateForwardMeteredDataInput([masterData]);

        var receiversWithMeteredData = _sut.GetReceiversWithMeteredDataFromMasterDataList(
            [masterData],
            forwardMeteredDataInput);

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
            [masterData],
            forwardMeteredDataInput);

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
            [masterData],
            forwardMeteredDataInput);

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
            [masterData],
            forwardMeteredDataInput);

        receiversWithMeteredData.Should()
            .ContainSingle()
            .Which.Actors
            .Should()
            .HaveCount(2)
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
                });
    }

    [Theory]
    [MemberData(nameof(GetAllResolutionsExceptMonthly))]
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
            masterDataList,
            forwardMeteredDataInput);

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
    [MemberData(nameof(GetAllResolutionsExceptMonthly))]
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
            masterDataList,
            forwardMeteredDataInput);

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
            masterDataList,
            forwardMeteredDataInput);

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

    // TODO: This part will be impl as part of #607
    // [Theory]
    // [InlineData(MeteringPointType.NetProduction)]
    // [InlineData(MeteringPointType.SupplyToGrid)]
    // [InlineData(MeteringPointType.ConsumptionFromGrid)]
    // [InlineData(MeteringPointType.WholesaleServicesInformation)]
    // [InlineData(MeteringPointType.OwnProduction)]
    // [InlineData(MeteringPointType.NetFromGrid)]
    // [InlineData(MeteringPointType.NetToGrid)]
    // [InlineData(MeteringPointType.TotalConsumption)]
    // [InlineData(MeteringPointType.Analysis)]
    // [InlineData(MeteringPointType.NotUsed)]
    // [InlineData(MeteringPointType.SurplusProductionGroup6)]
    // [InlineData(MeteringPointType.NetLossCorrection)]
    // [InlineData(MeteringPointType.OtherConsumption)]
    // [InlineData(MeteringPointType.OtherProduction)]
    // [InlineData(MeteringPointType.ExchangeReactiveEnergy)]
    // [InlineData(MeteringPointType.CollectiveNetProduction)]
    // [InlineData(MeteringPointType.CollectiveNetConsumption)]
    // public void Think_of_the_children()
    // {
    //     var meteringPointMasterData = GetMasterData(MeteringPointType.VeProduction);
    //
    //     var result = _sut.GetReceiversFromMasterData(meteringPointMasterData);
    //
    //     result.Actors.Should().HaveCount(2);
    //     result.Actors
    //         .OrderBy(a => a.ActorRole.Name)
    //         .ThenBy(a => a.ActorNumber.Value)
    //         .Should()
    //         .SatisfyRespectively(
    //             mar =>
    //             {
    //                 mar.ActorNumber.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
    //                 mar.ActorRole.Should().Be(ActorRole.DanishEnergyAgency);
    //             },
    //             mar =>
    //             {
    //                 mar.ActorNumber.Value.Should().Be(DataHubDetails.SystemOperatorNumber);
    //                 mar.ActorRole.Should().Be(ActorRole.SystemOperator);
    //             });
    // }

    private MeteringPointMasterData CreateMasterData(
        MeteringPointType? meteringPointType = null,
        Instant? from = null,
        Instant? to = null,
        Resolution? resolution = null,
        ActorNumber? gridAccessProvider = null,
        ActorNumber? energySupplier = null)
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
            ParentMeteringPointId: null,
            EnergySupplier: energySupplier ?? _defaultEnergySupplier);
    }

    private ForwardMeteredDataInputV1 CreateForwardMeteredDataInput(List<MeteringPointMasterData> masterData)
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
            DelegatedGridAreaCodes: [],
            MeteredDataList: meteredData);
    }
}
