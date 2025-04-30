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
using Moq;
using NodaTime;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.WorkingDays;

public class DataHubSupportCalenderTests
{
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;

    [Theory]
    // Basis tests
    [InlineData(2025, 4, 27, 22, 0, 2025, 4, 27, 22, 0, 0)] // 0 working days back or forward: 28th of April 2025 -> 28th of April 2025
    [InlineData(2025, 4, 24, 22, 0, 2025, 4, 27, 22, 0, 1)] // 1 working day forward: 25th of April 2025 -> 28th of April 2025 (Weekend)
    // Summer time
    [InlineData(2025, 4, 27, 22, 0, 2025, 4, 21, 22, 0, -4)] // 4 working days back: 28th of April 2025 -> 22nd of April 2025 (Weekend)
    [InlineData(2025, 4, 21, 22, 0, 2025, 4, 10, 22, 0, -4)] // 4 working days back: 22nd of April 2025 -> 11th of April 2025 (Easter)
    [InlineData(2025, 6, 10, 22, 0, 2025, 6, 3, 22, 0, -4)] // 4 working days back: 11th of June 2025 -> 4th of June 2025 (Pentecost Monday)
    // Transition
    [InlineData(2025, 4, 2, 22, 0, 2025, 3, 27, 23, 0, -4)] // 4 working days back: 2nd of April 2025 -> 28th of Marts 2025 (Transition)
    // Winter time
    [InlineData(2024, 12, 26, 23, 0, 2024, 12, 17, 23, 0, -4)] // 4 working days back: 27th of December 2024 -> 18th of December 2024 (Christmas)
    [InlineData(2024, 3, 4, 23, 0, 2024, 2, 27, 23, 0, -4)] // 4 working days back: 5th of Marts 2024 -> 28th of Marts 2024 (leap year)
    [InlineData(2025, 1, 2, 23, 0, 2024, 12, 22, 23, 0, -4)] // 4 working days back: 3rd of January 2025 -> 23rd of December 2024 (leap year)
    public void GetWorkingDay_WhenCount_ReturnsWorkingDateRelativeToToday(
        int actualYear,
        int actualMonth,
        int actualDay,
        int actualHour,
        int actualMinute,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        int expectedHour,
        int expectedMinute,
        int count)
    {
        // Arrange
        var testDate = Instant.FromUtc(actualYear, actualMonth, actualDay, actualHour, actualMinute);
        var expected = Instant.FromUtc(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute).InZone(_zone);
        var clock = new Mock<IClock>();
        clock.Setup(x => x.GetCurrentInstant()).Returns(testDate);
        var sut = new DataHubSupportCalender(clock.Object, _zone);

        // Act
        var actual = sut.GetWorkingDayRelativeToToday(count);

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
        var sut = new DataHubSupportCalender(clock.Object, _zone);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.GetWorkingDayRelativeToToday(1));
    }

    [Theory]
    [InlineData(-201)]
    [InlineData(201)]
    public void GetWorkingDay_WhenCountIsOutOfRange_ThrowsException(int count)
    {
        // Arrange
        var testDate = Instant.FromUtc(2025, 1, 1, 0, 0);
        var clock = new Mock<IClock>();
        clock.Setup(x => x.GetCurrentInstant()).Returns(testDate);
        var sut = new DataHubSupportCalender(clock.Object, _zone);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.GetWorkingDayRelativeToToday(count));
    }
}
