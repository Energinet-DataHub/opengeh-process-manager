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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.
    BusinessValidation;

public class PositionCountValidationRuleTests
{
    private readonly PositionCountValidationRule _sut = new();

    [Fact]
    public async Task QuarterHourly_PeriodNotMod()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T01:23:57Z")
            .WithEndDateTime("2023-01-01T02:53:56Z")
            .WithResolution(Resolution.QuarterHourly.Name)
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

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.PeriodNotModError(14.983333333333334));
    }

    [Fact]
    public async Task Hourly_PeriodNotMod()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T17:42:18Z")
            .WithEndDateTime("2023-01-01T19:41:54Z")
            .WithResolution(Resolution.Hourly.Name)
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

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.PeriodNotModError(0.9933333333333334));
    }

    [Fact]
    public async Task Daily_PeriodNotMod()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T11:02:19Z")
            .WithEndDateTime("2023-01-04T11:02:20Z")
            .WithResolution(Resolution.Daily.Name)
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
            .And.Contain(PositionCountValidationRule.PeriodNotModError(1.157407407426092E-05));
    }

    [Fact]
    public async Task Monthly_SecondsMinAndHoursNotMatch_WeDontCareSoItsFine()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T15:18:40Z")
            .WithEndDateTime("2023-03-01T16:19:41Z")
            .WithResolution(Resolution.Monthly.Name)
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

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError);
    }

    [Fact]
    public async Task Monthly_FirstOfMonthDaysNotMatch_PeriodNotMod()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-01T15:18:40Z")
            .WithEndDateTime("2023-03-02T15:18:40Z")
            .WithResolution(Resolution.Monthly.Name)
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

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.PeriodNotModError(0.25));
    }

    [Fact]
    public async Task Monthly_MiddleOfMonthDaysMatch_IsFine()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-15T15:18:40Z")
            .WithEndDateTime("2023-03-15T15:18:40Z")
            .WithResolution(Resolution.Monthly.Name)
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

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.IncorrectNumberOfPositionsError);
    }

    [Fact]
    public async Task Monthly_MiddleOfMonthDaysNotMatch_PeriodNotMod()
    {
        var inputV1 = new ForwardMeteredDataInputV1Builder()
            .WithStartDateTime("2023-01-15T15:18:40Z")
            .WithEndDateTime("2023-03-16T15:18:40Z")
            .WithResolution(Resolution.Monthly.Name)
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

        result.Should().ContainSingle().And.Contain(PositionCountValidationRule.PeriodNotModError(0.25));
    }
}
