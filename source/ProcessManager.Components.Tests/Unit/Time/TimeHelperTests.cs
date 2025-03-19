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

using Energinet.DataHub.ProcessManager.Components.Time;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Time;

public class TimeHelperTests
{
    [Theory]
    [InlineData(2025, 1, 2, 1, 1, 2025, 1, 1, 23, 0)] // 1st of January 2025 23:00
    [InlineData(2025, 1, 3, 23, 0, 2025, 1, 3, 23, 0)] // 3rd of January 2025 23:00
    [InlineData(2025, 7, 2, 1, 1, 2025, 7, 1, 22, 0)] // 1st of July 2025 22:00
    [InlineData(2025, 7, 2, 22, 1, 2025, 7, 2, 22, 0)] // 2nd of July 2025 22:00
    [InlineData(2025, 7, 2, 23, 1, 2025, 7, 2, 22, 0)] // 2nd of July 2025 22:00
    [InlineData(2025, 7, 2, 21, 1, 2025, 7, 1, 22, 0)] // 1st of July 2025 22:00
    [InlineData(2025, 1, 1, 1, 1, 2024, 12, 31, 23, 0)] // 1st of January 2025 23:00
    [InlineData(2024, 2, 29, 1, 1, 2024, 2, 28, 23, 0)] // 29th of February 2024 23:00
    [InlineData(2024, 3, 1, 1, 1, 2024, 2, 29, 23, 0)] // 1st of March 2024 23:00 Leap year
    [InlineData(2024, 3, 1, 0, 1, 2024, 2, 29, 23, 0)] // 1st of March 2024 23:00 Leap year
    [InlineData(2024, 3, 1, 0, 0, 2024, 2, 29, 23, 0)] // 1st of March 2024 23:00 Leap year
    public void TestGetMidnightZonedDateTime(int actualYear, int actualMonth, int actualDay, int actualHour, int actualMinute, int expectedYear, int expectedMonth, int expectedDay, int expectedHour, int expectedMinute)
    {
        // Arrange
        var instant = Instant.FromUtc(actualYear, actualMonth, actualDay, actualHour, actualMinute);
        var expected = Instant.FromUtc(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute);
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;
        var sut = new TimeHelper(zone);

        // Act
        var actual = sut.GetMidnightZonedDateTime(instant);

        // Assert
        actual.Should().Be(expected);
    }
}
