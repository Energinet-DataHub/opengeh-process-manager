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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_025.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Model;
using FluentAssertions;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_025.V1.BusinessValidation;

public class PeriodValidationRuleTests
{
    private readonly PeriodValidationRule _sut = GetPeriodValidationRule(Instant.FromUtc(2025, 1, 1, 0, 0, 0));

    [Theory]
    [InlineData("monkeykitten", "2024-01-31T00:00:00Z")]
    [InlineData("2024-01-01T00:00:00Z", "2024-42-01T00:00:00Z")]
    public async Task Given_InvalidStartAndEndDate_When_ValidateAsync_Then_Error(string startDate, string endDate)
    {
        // Arrange
        var request = new RequestMeasurementsBusinessValidatedDto(
            new RequestMeasurementsInputV1(
                "1234",
                "1234",
                "1111111111111",
                "DDQ",
                "2222222222222",
                startDate,
                endDate),
            []);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.Should().ContainSingle();
        result.Single().Message.Should().Be(PeriodValidationRule.InvalidStartOrEnDate);
        result.Single().ErrorCode.Should().Be("E50");
    }

    [Fact]
    public async Task Given_NoEndDate_When_ValidateAsync_Then_Error()
    {
        // Arrange
        var request = new RequestMeasurementsBusinessValidatedDto(
            new RequestMeasurementsInputV1(
                "1234",
                "1234",
                "1111111111111",
                "DDQ",
                "2222222222222",
                "2024-01-01T00:00:00Z",
                null),
            []);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.Should().ContainSingle();
        result.Single().Message.Should().Be(PeriodValidationRule.MissingStartOrEndDate);
        result.Single().ErrorCode.Should().Be("E50");
    }

    [Fact]
    public async Task Given_StartAfterEndDate_When_ValidateAsync_Then_Error()
    {
        // Arrange
        var request = new RequestMeasurementsBusinessValidatedDto(
            new RequestMeasurementsInputV1(
                "1234",
                "1234",
                "1111111111111",
                "DDQ",
                "2222222222222",
                "2024-01-01T23:00:00Z",
                "2023-12-01T23:00:00Z"),
            []);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.Should().ContainSingle();
        result.Single().Message.Should().Be(PeriodValidationRule.StartDateAfterEndDate);
        result.Single().ErrorCode.Should().Be("E50");
    }

    [Fact]
    public async Task Given_PeriodIsMoreThanAYear_When_ValidateAsync_Then_Error()
    {
        // Arrange
        var request = new RequestMeasurementsBusinessValidatedDto(
            new RequestMeasurementsInputV1(
                "1234",
                "1234",
                "1111111111111",
                "DDQ",
                "2222222222222",
                "2023-01-01T23:00:00Z",
                "2024-01-02T23:00:00Z"),
            []);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.Should().ContainSingle();
        result.Single().Message.Should().Be(PeriodValidationRule.PeriodIsTooLong);
        result.Single().ErrorCode.Should().Be("E50");
    }

    [Fact]
    public async Task Given_PeriodIsTooOld_When_ValidateAsync_Then_Error()
    {
        // Arrange
        var request = new RequestMeasurementsBusinessValidatedDto(
            new RequestMeasurementsInputV1(
                "1234",
                "1234",
                "1111111111111",
                "DDQ",
                "2222222222222",
                "2020-01-01T23:00:00Z",
                "2020-06-01T22:00:00Z"),
            []);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.Should().ContainSingle();
        result.Single().Message.Should().Be(PeriodValidationRule.PeriodIsTooOld);
        result.Single().ErrorCode.Should().Be("E50");
    }

    [Theory]
    [InlineData("2024-01-01T20:00:00Z", "2024-02-01T23:00:00Z")]
    [InlineData("2024-01-01T23:00:00Z", "2024-02-02T22:55:00Z")]
    public async Task Given_StartOrEndDateIsNotMidnight_When_ValidateAsync_Then_Error(
        string startDate,
        string endDate)
    {
        // Arrange
        var request = new RequestMeasurementsBusinessValidatedDto(
            new RequestMeasurementsInputV1(
                "1234",
                "1234",
                "1111111111111",
                "DDQ",
                "2222222222222",
                startDate,
                endDate),
            []);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.Should().ContainSingle();
        result.Single().Message.Should().Be(PeriodValidationRule.StartOrEndDateAreNotMidnight);
        result.Single().ErrorCode.Should().Be("E50");
    }

    private static PeriodValidationRule GetPeriodValidationRule(Instant now)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.GetCurrentInstant()).Returns(now);
        return new PeriodValidationRule(
            new PeriodValidator(DateTimeZoneProviders.Tzdb["Europe/Copenhagen"], clock.Object));
    }
}
