﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;
using FluentAssertions;
using FluentAssertions.Execution;
using NodaTime;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;

public class PeriodValidationRuleTests
{
    private static readonly ValidationError _invalidDateFormat =
        new(
            "Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT22:00:00Z eller YYYY-MM-DDT23:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT22:00:00Z or YYYY-MM-DDT23:00:00Z",
            "D66");

    private static readonly ValidationError _startDateMustBeLessThanOrEqualTo3YearsAnd3Months =
        new(
            "Der kan ikke anmodes om data for 3 år og 6 måneder tilbage i tid / It is not possible to request data 3 years and 6 months back in time",
            "E17");

    private static readonly ValidationError _invalidWinterMidnightFormat =
        new(
            "Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT23:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT23:00:00Z",
            "D66");

    private static readonly ValidationError _invalidSummerMidnightFormat =
        new(
            "Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT22:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT22:00:00Z",
            "D66");

    private static readonly ValidationError _invalidPeriodAcrossMonths =
        new(
            "Det er ikke muligt at anmode om data på tværs af måneder i forbindelse med en engrosfiksering eller korrektioner / It is not possible to request data across months in relation to wholesalefixing or corrections",
            "E17");

    private static readonly ValidationError _invalidPeriodLength =
        new(
            "Det er kun muligt at anmode om data på for en hel måned i forbindelse med en engrosfiksering eller korrektioner / It is only possible to request data for a full month in relation to wholesalefixing or corrections",
            "E17");

    private readonly PeriodValidationRule _sut;
    private readonly DateTimeZone _dateTimeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")
        ?? throw new InvalidOperationException("Couldn't find time zone");

    private Instant _now;

    public PeriodValidationRuleTests()
    {
        _now = Instant.FromUtc(2024, 5, 31, 22, 0, 0);
        _sut = new PeriodValidationRule(
            _dateTimeZone,
            new PeriodValidator(_dateTimeZone, new MockClock(() => _now)));
    }

    [Fact]
    public async Task Validate_WhenPeriodStartAndEndIsNonsense_ReturnsExpectedValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(
                "invalid-start-date",
                "invalid-end-date")
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        using var assertionScope = new AssertionScope();
        errors.Should().Satisfy(
            error =>
                error.Message.Contains(_invalidDateFormat.WithPropertyName("Period Start").Message)
                && error.ErrorCode.Equals(_invalidDateFormat.ErrorCode),
            error =>
                error.Message.Contains(_invalidDateFormat.WithPropertyName("Period End").Message)
                && error.ErrorCode.Equals(_invalidDateFormat.ErrorCode));
    }

    [Fact]
    public async Task Validate_WhenPeriodStartAndEndIsInAnInvalidFormat_ReturnsExpectedValidationErrors()
    {
        // Arrange
        var message1 = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod("2024-08-01", "2024-08-31")
            .Build();

        var message2 = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod("2024-08-01T23:00:00", "2024-08-31T23:00:00")
            .Build();

        // Act
        var errors1 = await _sut.ValidateAsync(message1);
        var errors2 = await _sut.ValidateAsync(message2);

        // Assert
        using var assertionScope = new AssertionScope();
        errors1.Should().Satisfy(
            error =>
                error.Message.Contains(_invalidDateFormat.WithPropertyName("Period Start").Message)
                && error.ErrorCode.Equals(_invalidDateFormat.ErrorCode),
            error =>
                error.Message.Contains(_invalidDateFormat.WithPropertyName("Period End").Message)
                && error.ErrorCode.Equals(_invalidDateFormat.ErrorCode));

        errors2.Should().Satisfy(
            error =>
                error.Message.Contains(_invalidDateFormat.WithPropertyName("Period Start").Message)
                && error.ErrorCode.Equals(_invalidDateFormat.ErrorCode),
            error =>
                error.Message.Contains(_invalidDateFormat.WithPropertyName("Period End").Message)
                && error.ErrorCode.Equals(_invalidDateFormat.ErrorCode));
    }

    [Fact]
    public async Task Validate_WhenPeriodStartIsMoreThan3YearsAnd6MonthsOldAndPeriodNotPartOfCutOffMonth_ReturnsExpectedValidationError()
    {
        // Arrange
        var dateTimeOffset = _now.ToDateTimeOffset().AddYears(-5);

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(
                dateTimeOffset.ToInstant(),
                dateTimeOffset.AddMonths(1).ToInstant())
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle().Subject.Should().Be(_startDateMustBeLessThanOrEqualTo3YearsAnd3Months);
    }

    [Fact]
    public async Task Validate_WhenPeriodStartIsLessThan3YearsAnd6MonthsOld_ReturnNoValidationError()
    {
        // Arrange
        _now = new LocalDateTime(2024, 6, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var start = new LocalDateTime(2022, 4, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var end = new LocalDateTime(2022, 5, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(start, end)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenPeriodStartIsMoreThan3And6MonthsBackInTimeButPartOfCutOffMonth_ReturnsNoValidationError()
    {
        // Arrange
        var periodStartDate = new LocalDateTime(2021, 6, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var periodEndDate = new LocalDateTime(2021, 7, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        _now = new LocalDateTime(2024, 12, 15, 13, 25, 37)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStartDate, periodEndDate)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        using var assertionScope = new AssertionScope();
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenPeriodOverlapSummerDaylightSavingTime_ReturnsNoValidationErrors()
    {
        // Arrange
        var winterTime = new LocalDateTime(_now.ToDateTimeOffset().Year, 3, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var summerTime = new LocalDateTime(_now.ToDateTimeOffset().Year, 4, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(winterTime, summerTime)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenPeriodOverlapWinterDaylightSavingTime_ReturnsNoValidationErrors()
    {
        // Arrange
        var summerTime = new LocalDateTime(_now.ToDateTimeOffset().Year, 10, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var winterTime = new LocalDateTime(_now.ToDateTimeOffset().Year, 11, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(summerTime, winterTime)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenStartHourIsWrong_ReturnsExpectedValidationError()
    {
        // Arrange
        var notWinterTimeMidnight = Instant.FromUtc(_now.ToDateTimeOffset().Year, 1, 1, 22, 0, 0);

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(
                notWinterTimeMidnight,
                Instant.FromUtc(_now.ToDateTimeOffset().Year, 1, 31, 23, 0, 0))
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors[0].Message.Should().Be(_invalidWinterMidnightFormat.WithPropertyName("Period Start").Message);
        errors[1].Message.Should().Be(_invalidPeriodAcrossMonths.Message);
    }

    [Fact]
    public async Task Validate_WhenEndHourIsWrong_ReturnsExpectedValidationError()
    {
        // Arrange
        var notSummerTimeMidnight = Instant.FromUtc(_now.ToDateTimeOffset().Year, 7, 31, 23, 0, 0);

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(
                Instant.FromUtc(_now.ToDateTimeOffset().Year, 6, 30, 22, 0, 0),
                notSummerTimeMidnight)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle().Subject.Should().Be(_invalidSummerMidnightFormat.WithPropertyName("Period End"));
    }

    [Fact]
    public async Task Validate_WhenPeriodEndIsMissing_ReturnsExpectedValidationError()
    {
        // Arrange
        var periodStart = Instant.FromUtc(_now.ToDateTimeOffset().Year, 1, 1, 23, 0, 0);

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStart, null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle().Subject.Should().Be(_invalidDateFormat.WithPropertyName("Period End"));
    }

    [Fact]
    public async Task Validate_WhenPeriodLongerThanOneMonth_ReturnsExpectedValidationError()
    {
        // Arrange
        var periodStartDate = new LocalDateTime(2021, 3, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var periodEndDate = new LocalDateTime(2021, 4, 30, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        _now = new LocalDateTime(2022, 5, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStartDate, periodEndDate)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle().Subject.Should().Be(_invalidPeriodAcrossMonths.WithPropertyName("Period End"));
    }

    [Fact]
    public async Task Validate_WhenPeriodShorterThanOneMonth_ReturnsExpectedValidationError()
    {
        // Arrange
        var periodStartDate = new LocalDateTime(2021, 3, 13, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var periodEndDate = new LocalDateTime(2021, 3, 17, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        _now = new LocalDateTime(2022, 5, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStartDate, periodEndDate)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle().Subject.Should().Be(_invalidPeriodLength.WithPropertyName("Period End"));
    }

    [Fact]
    public async Task Validate_WhenPeriodDoesNotStartOnTheFirstOfAMonth_ReturnsExpectedValidationError()
    {
        // Arrange
        var periodStartDate = new LocalDateTime(2021, 3, 13, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var periodEndDate = new LocalDateTime(2021, 3, 31, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        _now = new LocalDateTime(2022, 5, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStartDate, periodEndDate)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle().Subject.Should().Be(_invalidPeriodLength);
    }

    [Fact]
    public async Task Validate_WhenPeriodDoesNotEndOnTheLastDayOfAMonth_ReturnsExpectedValidationError()
    {
        // Arrange
        var periodStartDate = new LocalDateTime(2021, 3, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var periodEndDate = new LocalDateTime(2021, 3, 17, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        _now = new LocalDateTime(2022, 5, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStartDate, periodEndDate)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle().Subject.Should().Be(_invalidPeriodLength.WithPropertyName("Period End"));
    }

    [Fact]
    public async Task Validate_WhenPeriodExactlyOneMonth_ReturnsNoValidationError()
    {
        // Arrange
        var periodStartDate = new LocalDateTime(2021, 3, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var periodEndDate = new LocalDateTime(2021, 4, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        _now = new LocalDateTime(2022, 5, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStartDate, periodEndDate)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenPeriodExactlyTwoMonth_ReturnsExpectedValidationError()
    {
        // Arrange
        var periodStartDate = new LocalDateTime(2021, 3, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var periodEndDate = new LocalDateTime(2021, 5, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        _now = new LocalDateTime(2022, 5, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStartDate, periodEndDate)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle().Subject.Should().Be(_invalidPeriodAcrossMonths);
    }

    [Fact]
    public async Task Validate_WhenPeriodIsBetweenTwoYears_ReturnsNoValidationError()
    {
        // Arrange
        var periodStartDate = new LocalDateTime(2024, 12, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var periodEndDate = new LocalDateTime(2025, 1, 1, 0, 0, 0)
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStartDate, periodEndDate)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    private sealed class MockClock(Func<Instant> getInstant) : IClock
    {
        public Instant GetCurrentInstant() => getInstant.Invoke();
    }
}
