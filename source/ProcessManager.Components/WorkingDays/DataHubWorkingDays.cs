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
    private readonly DateTimeZone _dateTimeZone;

    public DataHubWorkingDays(IClock clock, DateTimeZone dateTimeZone)
    {
        _clock = clock;
        _dateTimeZone = dateTimeZone;
    }

    public Instant GetWorkingDayRelativeToToday(int count)
    {
        var date = _clock.GetCurrentInstant().InZone(_dateTimeZone);
        var direction = count < 0 ? -1 : 1;
        var remainingDays = Math.Abs(count);

        while (remainingDays > 0)
        {
            date = date.Plus(Duration.FromDays(direction));
            if (IsDataHubWorkingDay(date))
            {
                remainingDays--;
            }
        }

        return date.ToInstant();
    }

    private bool IsDataHubWorkingDay(ZonedDateTime zonedDateTime)
    {
        // Saturdays and Sundays are not working days.
        if (zonedDateTime.DayOfWeek is IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday)
            return false;

        // The 1st of January is not a working day.
        if (zonedDateTime is { Month: 1, Day: 1 })
            return false;

        // Maundy Thursday (Skærtorsdag) is not a working day.

        // Good Friday (Langfredag) is not a working day.

        // Easter Monday (2. Påskedag) is not a working day.

        // Pentecost Monday (2. Pinsedag) is not a working day.

        // 24th, 25th, 26th and 31st of December are not working days.
        if (zonedDateTime is { Month: 12, Day: 24 } or { Month: 12, Day: 25 } or { Month: 12, Day: 26 } or { Month: 12, Day: 31 })
            return false;

        return true;
    }
}
