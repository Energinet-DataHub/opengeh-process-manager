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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class MeteringPointTypeValidationRuleTests
{
    private readonly MeteringPointTypeValidationRule _sut = new();

    [Fact]
    public async Task Given_NoMasterData_When_Validate_Then_NoValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                []));

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Consumption")]
    [InlineData("Exchange")]
    [InlineData("VeProduction")]
    [InlineData("Analysis")]
    [InlineData("SurplusProductionGroup6")]
    [InlineData("NetProduction")]
    [InlineData("SupplyToGrid")]
    [InlineData("ConsumptionFromGrid")]
    [InlineData("WholesaleServicesInformation")]
    [InlineData("OwnProduction")]
    [InlineData("NetFromGrid")]
    [InlineData("NetToGrid")]
    [InlineData("TotalConsumption")]
    [InlineData("OtherConsumption")]
    [InlineData("OtherProduction")]
    [InlineData("ExchangeReactiveEnergy")]
    [InlineData("CollectiveNetProduction")]
    [InlineData("CollectiveNetConsumption")]
    [InlineData("InternalUse")]
    public async Task Given_ValidMeteringPointType_When_Validate_Then_NoValidationError(string meteringPointType)
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointType(meteringPointType)
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("id"),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        new GridAreaCode("111"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.FromName(meteringPointType),
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ElectricalHeating")]
    [InlineData("NetConsumption")]
    [InlineData("CapacitySettlement")]
    [InlineData("NotUsed")]
    [InlineData("NetLossCorrection")]
    public async Task Given_InvalidMeteringPointType_When_Validate_Then_ValidationError(string meteringPointType)
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointType(meteringPointType)
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("id"),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        new GridAreaCode("111"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.FromName(meteringPointType),
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointTypeValidationRule.WrongMeteringPointError);
    }

    [Fact]
    public async Task Given_ChangeBetweenTwoValidMeteringPointTypes_When_Validate_Then_ValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(new(
            input,
            [
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    // One MeteringPointType
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    Resolution.QuarterHourly,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    // A different MeteringPointType
                    MeteringPointType.Consumption,
                    MeteringPointSubType.Physical,
                    Resolution.QuarterHourly,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointTypeValidationRule.WrongMeteringPointError);
    }

    [Fact]
    public async Task Given_IncomingMeteringPointTypeDoesNotMatchMasterDataMeteringPointType_When_Validate_Then_ValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            // Incoming MeteringPointType
            .WithMeteringPointType("Production")
            .Build();

        var result = await _sut.ValidateAsync(new(
            input,
            [
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    // MeteringPointType different from incoming
                    MeteringPointType.Consumption,
                    MeteringPointSubType.Physical,
                    Resolution.QuarterHourly,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointTypeValidationRule.WrongMeteringPointError);
    }
}
