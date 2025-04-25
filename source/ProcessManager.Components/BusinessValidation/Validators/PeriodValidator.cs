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

using NodaTime;

namespace Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;

public class PeriodValidator(DateTimeZone dateTimeZone, IClock clock)
{
    private readonly DateTimeZone _dateTimeZone = dateTimeZone;
    private readonly IClock _clock = clock;

    public bool IsMidnight(Instant instant, out ZonedDateTime zonedDateTime)
    {
        zonedDateTime = new ZonedDateTime(instant, _dateTimeZone);

        return zonedDateTime.TimeOfDay == LocalTime.Midnight;
    }

    public bool IsDateOlderThanAllowed(Instant date, int maxYears, int maxMonths)
    {
        var dateInQuestion = date.InZone(_dateTimeZone);
        var someYearsAndMonthsAgo = _clock.GetCurrentInstant()
            .InZone(_dateTimeZone)
            .Date.PlusYears(-maxYears)
            .PlusMonths(-maxMonths);

        return dateInQuestion.LocalDateTime.Date < someYearsAndMonthsAgo;
    }

    public bool IsMonthOfDateOlderThanXYearsAndYMonths(Instant periodStart, int years, int months)
    {
        var dateInQuestion = periodStart.InZone(_dateTimeZone);
        var someYearsAndSomeMonthsAgo = _clock.GetCurrentInstant()
            .InZone(_dateTimeZone)
            .Date.PlusYears(-years)
            .PlusMonths(-months);

        if (dateInQuestion.Year > someYearsAndSomeMonthsAgo.Year)
            return false;

        if (dateInQuestion.Year == someYearsAndSomeMonthsAgo.Year)
            return dateInQuestion.Month < someYearsAndSomeMonthsAgo.Month;

        return true;
    }

    public bool IntervalMustBeLessThanAllowedPeriodSize(Instant start, Instant end, int maxAllowedPeriodSizeInMonths)
    {
        var actualEndDate = end.InZone(_dateTimeZone);
        var lastestAllowedEndDate = start.InZone(_dateTimeZone).LocalDateTime.PlusMonths(maxAllowedPeriodSizeInMonths);

        return actualEndDate.LocalDateTime > lastestAllowedEndDate;
    }
}
