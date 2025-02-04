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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Helpers;
using Energinet.DataHub.ProcessManager.Components.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;
using FluentAssertions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_026.V1.BusinessValidation.Rules;

public class PeriodValidatorTests
{
    private static readonly ValidationError _invalidDateFormat = new("Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT22:00:00Z eller YYYY-MM-DDT23:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT22:00:00Z or YYYY-MM-DDT23:00:00Z", "D66");
    private static readonly ValidationError _invalidWinterMidnightFormat = new("Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT23:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT23:00:00Z", "D66");
    private static readonly ValidationError _invalidSummerMidnightFormat = new("Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT22:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT22:00:00Z", "D66");
    private static readonly ValidationError _startDateMustBeLessThen3Years = new("Dato må max være 3 år og 6 måneder tilbage i tid / Can maximum be 3 years and 6 months back in time", "E17");
    private static readonly ValidationError _periodIsGreaterThenAllowedPeriodSize = new("Dato må kun være for 1 måned af gangen / Can maximum be for a 1 month period", "E17");
    private static readonly ValidationError _missingStartOrAndEndDate = new("Start og slut dato skal udfyldes / Start and end date must be present in request", "E50");

    private readonly PeriodValidationRule _sut = new(
        new PeriodValidationHelper(
            DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!,
            SystemClock.Instance));

    [Fact]
    public async Task Validate_WhenRequestIsValid_ReturnsNoValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.MeteredDataResponsible)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenEndDateIsUnspecified_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.MeteredDataResponsible)
            .WithPeriodEnd(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();
        var error = errors.First();
        error.Message.Should().Be(_missingStartOrAndEndDate.Message);
        error.ErrorCode.Should().Be(_missingStartOrAndEndDate.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenStartHourIsWrong_ReturnsExpectedValidationError()
    {
        // Arrange
        var now = SystemClock.Instance.GetCurrentInstant();
        var notWinterTimeMidnight = Instant.FromUtc(now.InUtc().Year, 1, 1, 22, 0, 0);
        var winterTimeMidnight = Instant.FromUtc(now.InUtc().Year, 1, 2, 23, 0, 0);
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithPeriod(
                periodStart: notWinterTimeMidnight,
                periodEnd: winterTimeMidnight)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();
        var error = errors.First();
        error.ErrorCode.Should().Be(_invalidWinterMidnightFormat.ErrorCode);
        error.Message.Should().Be(_invalidWinterMidnightFormat.WithPropertyName("Start date").Message);
    }

    [Fact]
    public async Task Validate_WhenEndHourIsWrong_ReturnsExpectedValidationError()
    {
        // Arrange
        var now = SystemClock.Instance.GetCurrentInstant();
        var notSummerTimeMidnight = Instant.FromUtc(now.InUtc().Year, 7, 1, 23, 0, 0);
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithPeriod(
                periodStart: Instant.FromUtc(now.InUtc().Year, 7, 2, 22, 0, 0),
                periodEnd: notSummerTimeMidnight)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();
        var error = errors.First();
        error.ErrorCode.Should().Be(_invalidSummerMidnightFormat.ErrorCode);
        error.Message.Should().Be(_invalidSummerMidnightFormat.WithPropertyName("End date").Message);
    }

    [Fact]
    public async Task Validate_WhenStartIsUnspecified_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithPeriod(periodStart: string.Empty, periodEnd: null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();
        var error = errors.First();
        error.ErrorCode.Should().Be(_missingStartOrAndEndDate.ErrorCode);
        error.Message.Should().Be(_missingStartOrAndEndDate.Message);
    }

    [Fact]
    public async Task Validate_WhenStartAndEndDateAreInvalid_ReturnsExpectedValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithPeriod(
                periodStart: "invalid-start-date",
                periodEnd: "invalid-end-date")
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Count.Should().Be(2);
        errors.Should().Contain(error => error.Message.Contains(_invalidDateFormat.WithPropertyName("Start date").Message)
                                         && error.ErrorCode.Equals(_invalidDateFormat.ErrorCode));
        errors.Should().Contain(error => error.Message.Contains(_invalidDateFormat.WithPropertyName("End date").Message)
                                         && error.ErrorCode.Equals(_invalidDateFormat.ErrorCode));
    }

    [Fact]
    public async Task Validate_WhenPeriodSizeIsGreaterThenAllowed_ReturnsExpectedValidationError()
    {
        // Arrange
        var now = SystemClock.Instance.GetCurrentInstant();
        var winterTimeMidnight = Instant.FromUtc(now.InUtc().Year, 1, 1, 23, 0, 0);
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithPeriod(
                winterTimeMidnight,
                winterTimeMidnight.Plus(Duration.FromDays(32)))
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();
        var error = errors.First();
        error.ErrorCode.Should().Be(_periodIsGreaterThenAllowedPeriodSize.ErrorCode);
        error.Message.Should().Be(_periodIsGreaterThenAllowedPeriodSize.Message);
    }

    [Fact]
    public async Task Validate_WhenPeriodIsOlderThenAllowed_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithPeriod(
                periodStart: Instant.FromUtc(2018, 1, 1, 23, 0, 0),
                periodEnd: Instant.FromUtc(2018, 1, 1, 23, 0, 0))
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();
        var error = errors.First();
        error.ErrorCode.Should().Be(_startDateMustBeLessThen3Years.ErrorCode);
        error.Message.Should().Be(_startDateMustBeLessThen3Years.Message);
    }

    [Fact]
    public async Task Validate_WhenPeriodOverlapSummerDaylightSavingTime_ReturnsNoValidationErrors()
    {
        // Arrange
        var winterTime = Instant.FromUtc(2023, 2, 26, 23, 0, 0);
        var summerTime = Instant.FromUtc(2023, 3, 26, 22, 0, 0);
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithPeriod(
                periodStart: winterTime,
                periodEnd: summerTime)
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
        var now = SystemClock.Instance.GetCurrentInstant();
        var summerTime = Instant.FromUtc(now.InUtc().Year, 9, 29, 22, 0, 0);
        var winterTime = Instant.FromUtc(now.InUtc().Year, 10, 29, 23, 0, 0);
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithPeriod(
                summerTime,
                winterTime)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }
}
