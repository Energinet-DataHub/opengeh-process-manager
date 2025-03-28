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
using FluentAssertions;
using Moq;
using NodaTime;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.BusinessValidation.Validators;

public class PeriodValidatorTests
{
    private static readonly DateTimeZone _dateTimeZone = DateTimeZoneProviders.Tzdb["Europe/Copenhagen"];

    [Fact]
    public void Given_ADateThatIsNotOlderThanAllowed_When_IsDateOlderThanAllowed_Then_DateIsNotOlder()
    {
        var dateThatIsNotTooOld = new LocalDateTime(2022, 4, 26, 11, 21, 0).InZoneStrictly(_dateTimeZone);
        var now = new LocalDateTime(2025, 6, 26, 11, 21, 0).InZoneStrictly(_dateTimeZone);

        var isDateOlderThanAllowed = GetPeriodValidator(now.ToInstant())
            .IsDateOlderThanAllowed(dateThatIsNotTooOld.ToInstant(), 3, 3);

        isDateOlderThanAllowed.Should().BeFalse("the date is not older than the allowed period");
    }

    [Fact]
    public void Given_ADateThatExactlyAsOldAsAllowed_When_IsDateOlderThanAllowed_Then_DateIsNotOlder()
    {
        var dateThatIsNotTooOld = new LocalDateTime(2022, 3, 26, 11, 21, 0).InZoneStrictly(_dateTimeZone);
        var now = new LocalDateTime(2025, 6, 26, 11, 21, 0).InZoneStrictly(_dateTimeZone);

        var isDateOlderThanAllowed = GetPeriodValidator(now.ToInstant())
            .IsDateOlderThanAllowed(dateThatIsNotTooOld.ToInstant(), 3, 3);

        isDateOlderThanAllowed.Should().BeFalse("the date is not older than the allowed period");
    }

    [Fact]
    public void
        Given_ADateThatIsTechnicallyToOldButOnTheLastAllowedDate_When_IsDateOlderThanAllowed_Then_DateIsNotOlder()
    {
        var dateThatIsNotTooOld = new LocalDateTime(2022, 3, 26, 8, 21, 0).InZoneStrictly(_dateTimeZone);
        var now = new LocalDateTime(2025, 6, 26, 21, 21, 0).InZoneStrictly(_dateTimeZone);

        var isDateOlderThanAllowed = GetPeriodValidator(now.ToInstant())
            .IsDateOlderThanAllowed(dateThatIsNotTooOld.ToInstant(), 3, 3);

        isDateOlderThanAllowed.Should().BeFalse("the date is not older than the allowed period");
    }

    [Fact]
    public void Given_ADateThatIsOlderThanAllowed_When_IsDateOlderThanAllowed_Then_DateIsOlder()
    {
        var dateThatIsNotTooOld = new LocalDateTime(2022, 3, 25, 11, 21, 0).InZoneStrictly(_dateTimeZone);
        var now = new LocalDateTime(2025, 6, 26, 11, 21, 0).InZoneStrictly(_dateTimeZone);

        var isDateOlderThanAllowed = GetPeriodValidator(now.ToInstant())
            .IsDateOlderThanAllowed(dateThatIsNotTooOld.ToInstant(), 3, 3);

        isDateOlderThanAllowed.Should().BeTrue("the date is older than the allowed period");
    }

    private PeriodValidator GetPeriodValidator(Instant now)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.GetCurrentInstant()).Returns(now);

        return new(_dateTimeZone, clock.Object);
    }
}
