﻿// Copyright 2020 Energinet DataHub A/S
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

namespace Energinet.DataHub.ProcessManager.Components.Time;

public class TimeHelper(DateTimeZone dateTimeZone)
{
    private readonly DateTimeZone _dateTimeZone = dateTimeZone;

    /// <summary>
    /// Transforms the given instant to midnight of the same day in the injected timezone.
    /// </summary>
    /// <param name="instant">The instant to transformed.</param>
    /// <returns>The new transformed instant.</returns>
    public Instant GetMidnightZonedDateTime(Instant instant)
    {
        var zonedDateTimeAtMidnight = instant
            .InZone(_dateTimeZone)
            .Date
            .AtMidnight();

        return zonedDateTimeAtMidnight
            .InZoneStrictly(_dateTimeZone)
            .ToInstant();
    }
}
