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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;

public static class InstantExtensions
{
    public static bool IsBefore(this Instant instant, Instant compareInstant)
    {
        return instant < compareInstant;
    }

    /// <summary>
    /// Checks whether the given instant is not a multiple of the given duration.
    /// E.g. 2024-01-01T00:00Z is a multiple of 1 hour, but 2024-01-01T00:01Z is not.
    /// </summary>
    public static bool IsNotMultipleOf(this Instant instant, Duration duration)
    {
        return instant.ToUnixTimeTicks() % duration.TotalTicks != 0;
    }
}
