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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Moq;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class PeriodValidationRuleTests
{
    private readonly PeriodValidationRule _sut;

    public PeriodValidationRuleTests()
    {
        _sut = GetPeriodValidationRule(Instant.FromUtc(2025, 1, 1, 0, 0, 0));
    }

    [Theory]
    [InlineData("00")]
    [InlineData("15")]
    [InlineData("30")]
    [InlineData("45")]
    public async Task Given_ValidMinutes_AndGiven_ResolutionIsQuarterHourly_When_ValidateAsync_Then_NoError(string minutes)
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime($"2025-01-01T23:{minutes}:00Z")
                    .WithEndDateTime($"2025-01-31T23:{minutes}:00Z")
                    .WithResolution(Resolution.QuarterHourly.Name)
                    .Build(),
                null,
                []));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_StartDateIsNotParsable_When_ValidateAsync_Then_Error()
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("invalid")
                    .Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.InvalidStartDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task Given_EndDateIsNotParsable_When_ValidateAsync_Then_Error(string? endDate)
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder().WithEndDateTime(endDate).Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.InvalidEndDate);
    }

    [Fact]
    public async Task Given_StartDateIsTooOld_When_ValidateAsync_Then_Error()
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("2021-11-30T23:00:00Z")
                    .WithEndDateTime("2021-12-31T23:00:00Z")
                    .Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.StartDateIsTooOld);
    }

    [Fact]
    public async Task Given_EndIsBeforeStart_When_ValidateAsync_Then_Error()
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("2025-02-01T23:00:00Z")
                    .WithEndDateTime("2025-01-31T23:00:00Z")
                    .Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.EndIsBeforeStart);
    }

    [Fact]
    public async Task Given_NotStartingAtAWholeQuarter_When_ValidateAsync_Then_Error()
    {
        var startWhichIsNotAWholeQuarter = InstantPattern.General.Parse("2025-01-01T23:05:00Z");

        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime(startWhichIsNotAWholeQuarter.Value.ToString())
                    .WithEndDateTime(startWhichIsNotAWholeQuarter.Value.PlusHours(4).ToString())
                    .WithResolution(Resolution.QuarterHourly.Name)
                    .Build(),
                null,
                []));

        result.Should().HaveCount(2)
            .And.BeEquivalentTo(
                [
                PeriodValidationRule.MinuteIsNotAWholeQuarter.WithPropertyName("start"),
                PeriodValidationRule.MinuteIsNotAWholeQuarter.WithPropertyName("end"),
                ]);
    }

    [Theory]
    [InlineData("QuarterHourly", "PT15M")]
    [InlineData("Hourly", "PT1H")]
    public async Task Given_PeriodOf3Hours_When_ValidateAsync_Then_Error(string resolution, string resolutionCode)
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("2025-03-24T22:00:00Z")
                    .WithEndDateTime("2025-03-25T01:00:00Z")
                    .WithResolution(resolution)
                    .Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.PeriodMustBeGreaterThan4Hours.WithPropertyName(resolutionCode));
    }

    [Theory]
    [InlineData("QuarterHourly")]
    [InlineData("Hourly")]
    public async Task Given_PeriodOf4Hours_AndGiven_SummerTimeToWinterTime_When_ValidateAsync_Then_NoError(string resolution)
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("2024-10-27T00:00:00Z") // 0 -> 1 -> 2 -> 2 -> 3 -> 4
                    .WithEndDateTime("2024-10-27T03:00:00Z") // The clock is set 1 hour backwards
                    .WithResolution(resolution)
                    .Build(),
                null,
                []));

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("QuarterHourly", "PT15M")]
    [InlineData("Hourly", "PT1H")]
    public async Task Given_PeriodOf3Hours_AndGiven_WinterTimeToSummerTime_When_ValidateAsync_Then_Error(string resolution, string resolutionCode)
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("2025-03-30T00:00:00Z") // 0 -> 1 -> 3 -> 4
                    .WithEndDateTime("2025-03-30T04:00:00Z") // The clock is set 1 hour forward
                    .WithResolution(resolution)
                    .Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.PeriodMustBeGreaterThan4Hours.WithPropertyName(resolutionCode));
    }

    [Fact]
    public async Task Given_PeriodOf1Month_AndGiven_StartAndIsNotFirstOfMonth_When_ValidateAsync_Then_Error()
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("2025-06-02T22:00:00Z")
                    .WithEndDateTime("2025-07-02T22:00:00Z")
                    .WithResolution(Resolution.Monthly.Name)
                    .Build(),
                null,
                []));

        result.Should()
            .HaveCount(2)
            .And.BeEquivalentTo(
            [
                PeriodValidationRule.IsNotFirstOfMonthMidnightSummertime.WithPropertyName("start"),
                PeriodValidationRule.IsNotFirstOfMonthMidnightSummertime.WithPropertyName("end"),
            ]);
    }

    [Fact]
    public async Task Given_PeriodOf1Month_AndGiven_StartAndEndIsNotMidnight_When_ValidateAsync_Then_Error()
    {
        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("2025-06-30T22:05:00Z")
                    .WithEndDateTime("2025-07-31T22:05:00Z")
                    .WithResolution(Resolution.Monthly.Name)
                    .Build(),
                null,
                []));

        result.Should()
            .HaveCount(2)
            .And.BeEquivalentTo(
            [
                PeriodValidationRule.IsNotFirstOfMonthMidnightSummertime.WithPropertyName("start"),
                PeriodValidationRule.IsNotFirstOfMonthMidnightSummertime.WithPropertyName("end"),
            ]);
    }

    private PeriodValidationRule GetPeriodValidationRule(Instant now)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.GetCurrentInstant()).Returns(now);
        return new PeriodValidationRule(
            new PeriodValidator(DateTimeZoneProviders.Tzdb["Europe/Copenhagen"], clock.Object));
    }
}
