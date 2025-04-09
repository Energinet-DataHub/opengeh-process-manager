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

public class ResolutionValidationRuleTests
{
    private readonly ResolutionValidationRule _sut = new();

    [Fact]
    public async Task Given_NoMasterData_When_Validate_Then_NoValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                null,
                []));

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("QuarterHourly")]
    [InlineData("Hourly")]
    public async Task Given_ResolutionIsValid_When_Validate_Then_NoValidationError(string resolution)
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(resolution)
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                null,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("id"),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        new GridAreaCode("111"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Production,
                        MeteringPointSubType.Physical,
                        Resolution.FromName(resolution),
                        MeasurementUnit.KilowattHour,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MonthlyResolutionAndMeteringPointTypeIsVeProduction_When_Validate_Then_NoValidationError()
    {
        var monthlyResolution = Resolution.Monthly;
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(monthlyResolution.Name)
            .WithMeteringPointType(MeteringPointType.VeProduction.Name)
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                null,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("id"),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        new GridAreaCode("111"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.VeProduction,
                        MeteringPointSubType.Physical,
                        monthlyResolution,
                        MeasurementUnit.KilowattHour,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Daily")]
    public async Task Given_InvalidResolution_When_Validate_Then_ValidationError(string resolution)
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(resolution)
            .Build();

        var result = await _sut.ValidateAsync(new(
            input,
            null,
            [
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    Resolution.FromName(resolution),
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }

    [Fact]
    public async Task Given_MasterDataHasTwoDifferentResolutions_When_Validate_Then_ValidationError()
    {
        var validResolution = Resolution.QuarterHourly;
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(validResolution.Name)
            .Build();

        var result = await _sut.ValidateAsync(new(
            input,
            null,
            [
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    validResolution,
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
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    // Different resolution
                    Resolution.Hourly,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }

    [Fact]
    public async Task Given_ResolutionIsMonthlyAndMasterDataMeteringPointIsNotVeProduction_When_Validate_Then_ValidationError()
    {
        var validResolutionForVeProduction = Resolution.Monthly;
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointType(MeteringPointType.VeProduction.Name)
            .WithResolution(validResolutionForVeProduction.Name)
            .Build();

        var result = await _sut.ValidateAsync(new(
            input,
            null,
            [
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    // Not ve production metering point type
                    MeteringPointType.Consumption,
                    MeteringPointSubType.Physical,
                    validResolutionForVeProduction,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }

    [Fact]
    public async Task Given_ResolutionIsMonthlyAndInputIsNotProduction_When_Validate_Then_ValidationError()
    {
        var validResolutionForVeProduction = Resolution.Monthly;
        var input = new ForwardMeteredDataInputV1Builder()
            // Not ve production metering point type
            .WithMeteringPointType(MeteringPointType.Consumption.Name)
            .WithResolution(validResolutionForVeProduction.Name)
            .Build();

        var result = await _sut.ValidateAsync(new(
            input,
            null,
            [
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    validResolutionForVeProduction,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }
}
