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

using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Components.Time;

public class TimeHelper(DateTimeZone dateTimeZone)
{
    private readonly DateTimeZone _dateTimeZone = dateTimeZone;

    /// <summary>
    /// Transforms the given instant to the midnight of the same day in the given timezone.
    /// </summary>
    /// <param name="instant">The instance to transformed.</param>
    /// <returns>The new transformed instance.</returns>
    public Instant GetMidnightZonedDateTime(Instant instant)
    {
        var zonedDateTime = new ZonedDateTime(instant, _dateTimeZone);
        var adjustedHour = 24 - (zonedDateTime.IsDaylightSavingTime() ? 2 : 1);

        // Return instance with adjusted hour
        var adjustedInstant = Instant.FromUtc(
            zonedDateTime.Year,
            zonedDateTime.Month,
            zonedDateTime.Day,
            adjustedHour,
            0,
            0);
        return adjustedInstant.PlusDays(-1);
    }
}
