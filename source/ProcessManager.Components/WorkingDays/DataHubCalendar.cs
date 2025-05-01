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

namespace Energinet.DataHub.ProcessManager.Components.WorkingDays;

public class DataHubCalendar
{
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public DataHubCalendar(IClock clock, DateTimeZone zone)
    {
        _clock = clock;
        _zone = zone;
    }

    public Instant CurrentDate()
    {
        return _clock.GetCurrentInstant().InZone(_zone).ToInstant();
    }

    /// <summary>
    /// Calculates the DataHub working date back in time relative to today.
    /// </summary>
    /// <param name="count">The number of days back in time. This figure must be between 0 and 100 inclusive.</param>
    /// <returns>A DataHub working date relative to today.</returns>
    public LocalDate GetWorkingDayRelativeToTodayBackInTime(int count)
    {
        if (count is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(count), $"Count {count} must be between 0 and 100.");
        }

        var remainingDays = count;
        var currentDate = _clock.GetCurrentInstant().InZone(_zone).LocalDateTime.Date;
        var easterSunday = CalculateEasterSunday(currentDate.Year);

        while (remainingDays > 0)
        {
            currentDate = currentDate.Plus(Period.FromDays(-1));
            if (IsDataHubWorkingDay(currentDate, easterSunday))
            {
                remainingDays--;
            }
        }

        // Return the start of the day in the given timezone.
        return currentDate.AtMidnight().Date;
    }

    private bool IsDataHubWorkingDay(LocalDate currentDate, LocalDate easterSunday)
    {
        // The following days are not DataHub working days.

        // Saturdays and Sundays.
        if (currentDate.DayOfWeek is IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday)
            return false;

        // New years day (Nytårsdag).
        if (currentDate is { Month: 1, Day: 1 })
            return false;

        // Maundy Thursday (Skærtorsdag), Good Friday (Langfredag), and Easter Monday (2. Påskedag).
        var maundyThursday = easterSunday.Minus(Period.FromDays(3));
        var goodFriday = easterSunday.Minus(Period.FromDays(2));
        var easterMonday = easterSunday.Plus(Period.FromDays(1));
        if (maundyThursday == currentDate ||
            goodFriday == currentDate ||
            easterMonday == currentDate)
        {
            return false;
        }

        // Ascension Day (Kristi Himmelfartsdag) and the day after.
        // Ascension Day is always 40 days after Easter Sunday.
        // Note that days are counted in liturgical (church) tradition versus modern date calculation.
        // Why it's 39 days between, but the 40th day after: Easter Sunday is counted as Day 1 in the Christian liturgical tradition.
        // So when we say "40 days after Easter", it includes Easter Sunday itself in the count.
        // https://da.wikipedia.org/wiki/Kristi_himmelfartsdag
        var ascensionDay = easterSunday.Plus(Period.FromDays(39));
        var dayAfterAscensionDay = ascensionDay.Plus(Period.FromDays(1));
        if (ascensionDay == currentDate || dayAfterAscensionDay == currentDate)
            return false;

        // Pentecost Monday (2. Pinsedag).
        // Pentecost Monday is always 50 days after Easter Sunday.
        // https://natmus.dk/historisk-viden/temaer/fester-og-traditioner/pinse/
        var pentecostMonday = easterSunday.Plus(Period.FromDays(50));
        if (pentecostMonday == currentDate)
            return false;

        // Christmas and New Years eve. 24th, 25th, 26th, and 31st of December.
        if (currentDate is { Month: 12, Day: 24 or 25 or 26 or 31 })
            return false;

        return true;
    }

    /// <summary>
    /// Calculates Easter Sunday for a given year using the Anonymous Gregorian algorithm.
    /// https://www.experimentarium.dk/faenomener/saadan-falder-paasken-hvert-aar/
    /// </summary>
    /// <param name="year">The year to calculate Easter sunday for.</param>
    /// <returns>The date of the Easter Sunday for the given year.</returns>
    private LocalDate CalculateEasterSunday(int year)
    {
        if (year is < 1800 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be between 1800 and 2200.");

        var a = year % 19;
        var b = year % 4;
        var c = year % 7;

        int m, n;
        if (year <= 1899)
        {
            m = 23;
            n = 4;
        }
        else if (year <= 2099)
        {
            m = 24;
            n = 5;
        }
        else
        {
            // 2100–2199
            m = 24;
            n = 6;
        }

        var d = ((19 * a) + m) % 30;
        var e = ((2 * b) + (4 * c) + (6 * d) + n) % 7;
        int day;
        int month;

        if (22 + d + e <= 31)
        {
            month = 3; // March
            day = 22 + d + e;
        }
        else
        {
            month = 4; // April
            day = d + e - 9;
        }

        // Exceptions
        if (d == 29 && e == 6)
        {
            day = 19;
            month = 4;
        }
        else if (d == 28 && e == 6 && a > 10)
        {
            day = 18;
            month = 4;
        }

        return new LocalDate(year, month, day).AtMidnight().Date;
    }
}
