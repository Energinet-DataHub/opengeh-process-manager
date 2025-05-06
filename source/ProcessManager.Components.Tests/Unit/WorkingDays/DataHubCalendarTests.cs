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

using Energinet.DataHub.ProcessManager.Components.WorkingDays;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Moq;
using NodaTime;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.WorkingDays;

public class DataHubCalendarTests
{
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;

    [Theory]
    // Basis tests
    [InlineData(2025, 4, 28, 2, 2025, 4, 27, 22, 0, 0)] // 0 working day back: 28th of April 2025 -> 28th of April 2025

    // Weekend
    [InlineData(2025, 4, 28, 5, 2025, 4, 22, 22, 0, 3)] // 3 working days back: 28th of April 2025 -> 23rd of April 2025

    // Leap year
    [InlineData(2024, 3, 4, 23, 2024, 2, 28, 23, 0, 3)] // 3 working days back: 5th of Marts 2024 -> 29th of February 2024
    [InlineData(2025, 3, 4, 23, 2025, 2, 27, 23, 0, 3)] // 3 working days back: 5th of Marts 2025 -> 28th of February 2025

    // Easter
    [InlineData(2025, 4, 21, 22, 2025, 4, 13, 22, 0, 3)] // 3 working days back: 22nd of April 2025 -> 14th of April 2025
    [InlineData(2025, 4, 20, 22, 2025, 4, 13, 22, 0, 3)] // 3 working days back: 21st of April 2025 -> 14th of April 2025
    [InlineData(2025, 4, 16, 22, 2025, 4, 13, 22, 0, 3)] // 3 working days back: 17th of April 2025 -> 14th of April 2025

    // Ascension Day (Kristi Himmelfartsdag) and the day after
    [InlineData(2025, 6, 1, 22, 2025, 5, 25, 22, 0, 3)] // 3 working days back: 2nd of June 2025 -> 26th of May 2025
    [InlineData(2025, 5, 28, 22, 2025, 5, 25, 22, 0, 3)] // 3 working days back: 29th of May 2025 -> 26th of May 2025

    // Pentecost Monday (2. Pinsedag)
    [InlineData(2025, 6, 10, 22, 2025, 6, 4, 22, 0, 3)] // 3 working days back: 11th of June 2025 -> 5th of June 2025
    [InlineData(2025, 6, 11, 22, 2025, 6, 5, 22, 0, 3)] // 3 working days back: 12th of June 2025 -> 6th of June 2025

    // Daylight saving time
    [InlineData(2025, 4, 1, 22, 2025, 3, 27, 23, 0, 3)] // 3 working days back: 1st of April 2025 -> 28th of Marts 2025

    // Christmas 24th, 25th, and 26th of December
    [InlineData(2024, 12, 23, 23, 2024, 12, 18, 23, 0, 3)] // 3 working days back: 24th of December 2024 -> 19th of December 2024
    [InlineData(2024, 12, 26, 23, 2024, 12, 18, 23, 0, 3)] // 3 working days back: 27th of December 2024 -> 19th of December 2024

    // New Year
    [InlineData(2025, 1, 2, 23, 2024, 12, 26, 23, 0, 3)] // 3 working days back: 3rd of January 2025 -> 27th of December 2024
    [InlineData(2024, 12, 31, 23, 2024, 12, 22, 23, 0, 3)] // 3 working days back: 1st of January 2025 -> 23th of December 2024
    [InlineData(2024, 12, 30, 23, 2024, 12, 22, 23, 0, 3)] // 3 working days back: 31st of December 2024 -> 23th of December 2024

    // Not midnight
    [InlineData(2025, 4, 27, 13, 2025, 4, 22, 22, 0, 3)] // 3 working days back: 28th of April 2025 -> 23rd of April 2025

    public void GetWorkingDay_WhenCount_ReturnsWorkingDateRelativeToToday(
        int actualYear,
        int actualMonth,
        int actualDay,
        int actualHour,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        int expectedHour,
        int expectedMinute,
        int count)
    {
        // Arrange
        var testDate = Instant.FromUtc(actualYear, actualMonth, actualDay, actualHour, 0);
        var expected = Instant.FromUtc(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute)
            .InZone(_zone)
            .ToInstant();

        var clock = new Mock<IClock>();
        clock.Setup(x => x.GetCurrentInstant()).Returns(testDate);
        var sut = new DataHubCalendar(clock.Object, _zone);

        // Act
        var actual = sut.GetWorkingDayRelativeToTodayBackInTime(count);

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(1799)]
    [InlineData(2201)]
    public void GetWorkingDay_WhenYearIsOutOfRange_ThrowsException(int year)
    {
        // Arrange
        var testDate = Instant.FromUtc(year, 1, 1, 0, 0);
        var clock = new Mock<IClock>();
        clock.Setup(x => x.GetCurrentInstant()).Returns(testDate);
        var sut = new DataHubCalendar(clock.Object, _zone);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.GetWorkingDayRelativeToTodayBackInTime(1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void GetWorkingDay_WhenCountIsOutOfRange_ThrowsException(int count)
    {
        // Arrange
        var testDate = Instant.FromUtc(2025, 1, 1, 0, 0);
        var clock = new Mock<IClock>();
        clock.Setup(x => x.GetCurrentInstant()).Returns(testDate);
        var sut = new DataHubCalendar(clock.Object, _zone);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.GetWorkingDayRelativeToTodayBackInTime(count));
    }

    [Theory]
    // Basis tests
    [InlineData(2025, 2, 1, 15, 12, 54, 2025, 1, 31, 23)]
    // Daylight saving time
    [InlineData(2025, 3, 30, 2, 0, 0, 2025, 3, 29, 23)]
    [InlineData(2025, 3, 30, 4, 0, 0, 2025, 3, 29, 23)]
    [InlineData(2025, 3, 30, 21, 59, 59, 2025, 3, 29, 23)]
    [InlineData(2025, 3, 30, 22, 0, 0, 2025, 3, 30, 22)]
    // Leap year
    [InlineData(2024, 3, 1, 22, 59, 59, 2024, 2, 29, 23)]
    [InlineData(2024, 3, 1, 23, 0, 0, 2024, 3, 1, 23)]
    // New year
    [InlineData(2025, 1, 1, 0, 0, 0, 2024, 12, 31, 23)]
    [InlineData(2025, 1, 2, 22, 59, 49, 2025, 1, 1, 23)]
    public void GetCurrentDay_ReturnsCurrentDayInUtc(
        int actualYear,
        int actualMonth,
        int actualDay,
        int actualHour,
        int actualMinute,
        int actualSecond,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        int expectedHour)
    {
        // Arrange
        var testDate = Instant.FromUtc(actualYear, actualMonth, actualDay, actualHour, actualMinute, actualSecond);
        var clock = new Mock<IClock>();
        clock.Setup(x => x.GetCurrentInstant()).Returns(testDate);
        var sut = new DataHubCalendar(clock.Object, _zone);

        // Act
        var actual = sut.CurrentDay();

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Year().Should().Be(expectedYear);
        actual.Month().Should().Be(expectedMonth);
        actual.Day().Should().Be(expectedDay);
        actual.Hour().Should().Be(expectedHour);
        actual.Minute().Should().Be(0);
        actual.Second().Should().Be(0);
    }
}
