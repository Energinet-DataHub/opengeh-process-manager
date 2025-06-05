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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.
    BusinessValidation;

public class PositionCountValidationRuleTests
{
    private readonly PositionCountValidationRule _sut = new();

    [Fact]
    public async Task Given_EmptyMeteredData_When_Validate_Then_PositionCountError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T01:23:56Z")
            .WithEndDateTime("2023-01-01T02:53:56Z")
            .WithResolution(Resolution.QuarterHourly.Name)
            .WithMeteredData([])
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(0, 6));
    }

    [Fact]
    public async Task Given_QuarterHourlyResolutionWithWrongPeriod_When_Validate_Then_ResidualError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T01:23:57Z")
            .WithEndDateTime("2023-01-01T02:53:56Z")
            .WithResolution(Resolution.QuarterHourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 6)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .ContainSingle()
            .And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(6, 5.998888888888889));
    }

    [Fact]
    public async Task Given_QuarterHourlyResolutionWithWrongCount_When_Validate_Then_PositionCountError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T01:23:56Z")
            .WithEndDateTime("2023-01-01T02:53:56Z")
            .WithResolution(Resolution.QuarterHourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 7)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(7, 6));
    }

    [Fact]
    public async Task Given_QuarterHourlyResolutionWithCorrectPeriodAndCount_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T01:23:56Z")
            .WithEndDateTime("2023-01-01T02:53:56Z")
            .WithResolution(Resolution.QuarterHourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 6)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MontlyResolutionWithCorrectPeriodBothAtTheEndOfTheMonthAndCount_When_Validate_Then_NoValidationError2()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2025-02-28T23:00:00Z")
            .WithEndDateTime("2025-03-31T22:00:00Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 1)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.Monthly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_HourlyResolutionAndWrongPeriod_When_Validate_Then_ResidualError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T17:42:18Z")
            .WithEndDateTime("2023-01-01T19:41:54Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 2)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .ContainSingle()
            .And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(2, 1.9933333333333334));
    }

    [Fact]
    public async Task Given_HourlyResolutionWithWrongCount_When_Validate_Then_CountError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T17:42:18Z")
            .WithEndDateTime("2023-01-01T19:42:18Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 4)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(4, 2));
    }

    [Fact]
    public async Task Given_HourlyResolutionWithCorrectPeriodAndCount_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T17:42:18Z")
            .WithEndDateTime("2023-01-01T19:42:18Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 2)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_DailyResolutionWithWrongPeriod_When_Validate_Then_ResidualError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T11:02:19Z")
            .WithEndDateTime("2023-01-04T11:02:20Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 3)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .ContainSingle()
            .And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(3, 3.0000115740740743));
    }

    [Fact]
    public async Task Given_DailyResolutionWithWrongCount_When_Validate_Then_CountError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T11:02:19Z")
            .WithEndDateTime("2023-01-04T11:02:19Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 4)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .ContainSingle()
            .And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(4, 3));
    }

    [Fact]
    public async Task Given_DailyResolutionWithCorrectPeriodAndCount_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T11:02:19Z")
            .WithEndDateTime("2023-01-04T11:02:19Z")
            .WithResolution(Resolution.Daily.Name)
            .WithMeteredData(
                Enumerable.Range(1, 3)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task
        Given_MonthlyResolutionWithStartOfMonthWhereSecondsMinutesAndHoursDoNotMatch_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T15:18:40Z")
            .WithEndDateTime("2023-03-01T16:19:41Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 2)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task
        Given_MonthlyResolutionWithEndOfMonthWhereSecondsMinutesAndHoursDoNotMatch_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-31T23:59:59Z")
            .WithEndDateTime("2023-04-30T23:59:59Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 3)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task
        Given_MonthlyResolutionWithMiddleOfMonthWhereSecondsMinutesAndHoursDoNotMatch_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-21T15:18:40Z")
            .WithEndDateTime("2023-03-21T16:19:41Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 2)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MonthlyResolutionWithStartOfMonthWhereDaysDoNotMatch_When_Validate_Then_ResidualError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T15:18:40Z")
            .WithEndDateTime("2023-03-02T15:18:40Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 2)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .ContainSingle()
            .And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(2, 2.01));
    }

    [Fact]
    public async Task Given_MonthlyResolutionWithMiddleOfMonthWhereDaysDoMatch_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-15T15:18:40Z")
            .WithEndDateTime("2023-03-15T15:18:40Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 2)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MonthlyResolutionWithEndOfMonthWhereDaysDoNotMatch_When_Validate_Then_ResidualError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-29T15:18:40Z")
            .WithEndDateTime("2023-04-30T15:18:40Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 3)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .ContainSingle()
            .And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(3, 3.01));
    }

    [Fact]
    public async Task
        Given_MonthlyResolutionWithEndOfMonthWhereOneDayIsEndOfMonthAndOneIsMiddleOfMonth_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-31T15:18:40Z")
            .WithEndDateTime("2023-04-30T15:18:40Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 3)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MonthlyResolutionWhenMiddleOfMonthWhereDaysDoNotMatch_When_Validate_Then_ResidualError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-15T15:18:40Z")
            .WithEndDateTime("2023-03-16T15:18:40Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 2)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .ContainSingle()
            .And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(2, 2.01));
    }

    [Fact]
    public async Task Given_MonthlyResolutionWithStartOfMonthWithWrongCount_When_Validate_Then_CountError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T15:18:40Z")
            .WithEndDateTime("2023-03-01T16:19:41Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 3)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(3, 2));
    }

    [Fact]
    public async Task Given_MonthlyResolutionWithMiddleOfMonthWithWrongCount_When_Validate_Then_CountError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-05-17T15:18:40Z")
            .WithEndDateTime("2023-07-17T16:19:41Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 3)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(3, 2));
    }

    [Fact]
    public async Task Given_MonthlyResolutionWithEndOfMonthWithWrongCount_When_Validate_Then_CountError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-31T15:18:40Z")
            .WithEndDateTime("2023-04-30T16:19:41Z")
            .WithResolution(Resolution.Monthly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 4)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError(4, 3));
    }

    [Fact]
    public async Task Given_PositionsAreShuffledButOtherwiseValid_When_Validate_Then_NoValidationError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T01:23:56Z")
            .WithEndDateTime("2023-01-01T11:23:56Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
                Enumerable.Range(1, 10)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(i.ToString(), "1024", Quality.AsProvided.Name))
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList())
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MissingPosition_When_Validate_Then_PositionsNotConsecutiveError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T01:23:56Z")
            .WithEndDateTime("2023-01-01T11:23:56Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
            [
                .. Enumerable.Range(1, 9)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            i.ToString(),
                            "1024",
                            Quality.AsProvided.Name)),
                new ForwardMeteredDataInputV1.MeteredData("11", "1024", Quality.AsProvided.Name)
            ])
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.PositionsNotConsecutiveError([10]));
    }

    [Fact]
    public async Task Given_APositionIsDuplication_When_Validate_Then_DuplicationAndConsecutiveError()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T01:23:56Z")
            .WithEndDateTime("2023-01-01T11:23:56Z")
            .WithResolution(Resolution.Hourly.Name)
            .WithMeteredData(
            [
                .. Enumerable.Range(1, 9)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            i.ToString(),
                            "1024",
                            Quality.AsProvided.Name)),
                new ForwardMeteredDataInputV1.MeteredData("9", "1024", Quality.AsProvided.Name)
            ])
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                inputV1,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("123456789012345678"),
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        new GridAreaCode("804"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Consumption,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "productId",
                        null,
                        ActorNumber.Create("2222222222222")),
                ]));

        result.Should()
            .HaveCount(2)
            .And.Contain(PositionCountValidationRule.DuplicatedPositionError([9]))
            .And.Contain(PositionCountValidationRule.PositionsNotConsecutiveError([10]));
    }
}
