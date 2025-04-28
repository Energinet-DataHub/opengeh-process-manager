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

public class DataHubWorkingDays
{
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;
    private ZonedDateTime _easterSunday;

    public DataHubWorkingDays(IClock clock, DateTimeZone zone)
    {
        _clock = clock;
        _zone = zone;
    }

    public ZonedDateTime GetWorkingDayRelativeToToday(int count)
    {
        var direction = count < 0 ? -1 : 1;
        var remainingDays = Math.Abs(count);
        var currentDate = _clock.GetCurrentInstant().InZone(_zone);
        _easterSunday = CalculateEaster(currentDate.Year);

        while (remainingDays > 0)
        {
            currentDate = currentDate.Plus(Duration.FromDays(direction));
            if (IsDataHubWorkingDay(currentDate))
            {
                Console.WriteLine(currentDate);
                remainingDays--;
            }
        }

        return currentDate.ToInstant().InZone(_zone);
    }

    private bool IsDataHubWorkingDay(ZonedDateTime zonedDateTime)
    {
        // The following holidays are not DataHub working days.

        // Saturdays and Sundays.
        if (zonedDateTime.DayOfWeek is IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday)
            return false;

        // New years day (Nytårsdag).
        if (zonedDateTime is { Month: 1, Day: 1 })
            return false;

        // Maundy Thursday (Skærtorsdag), Good Friday (Langfredag), and Easter Monday (2. Påskedag).
        if (_easterSunday.Minus(Duration.FromDays(3)) == zonedDateTime ||
            _easterSunday.Minus(Duration.FromDays(2)) == zonedDateTime ||
            _easterSunday.Plus(Duration.FromDays(1)) == zonedDateTime)
        {
            return false;
        }

        // Pentecost Monday (2. Pinsedag). Pentecost Monday is always 50 days after Easter Sunday.
        // https://natmus.dk/historisk-viden/temaer/fester-og-traditioner/pinse/
        if (_easterSunday.Plus(Duration.FromDays(50)) == zonedDateTime)
            return false;

        // Christmas and New Year. 24th, 25th, 26th, and 31st of December.
        if (zonedDateTime is { Month: 12, Day: 24 or 25 or 26 or 31 })
            return false;

        return true;
    }

    /// <summary>
    /// This method calculates Easter Sunday for a given year using the Anonymous Gregorian algorithm.
    /// https://www.experimentarium.dk/faenomener/saadan-falder-paasken-hvert-aar/
    /// </summary>
    /// <param name="year">The year to calculate Easter sunday.</param>
    /// <returns>The date of the Easter Sunday for the given year.</returns>
    private ZonedDateTime CalculateEaster(int year)
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

        return Instant
            .FromUtc(year, month, day, 0, 0, 0)
            .InZone(_zone).Date
            .AtStartOfDayInZone(_zone);
    }
}
