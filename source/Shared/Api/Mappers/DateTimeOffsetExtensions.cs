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

namespace Energinet.DataHub.ProcessManager.Shared.Api.Mappers;

internal static class DateTimeOffsetExtensions
{
    /// <summary>
    /// DateTimeOffset values must be in "round-trip" ("o"/"O") format to be parsed correctly
    /// See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier
    /// </summary>
    public static Instant? ToNullableInstant(this DateTimeOffset? dateTimeOffset)
    {
        return dateTimeOffset.HasValue
            ? Instant.FromDateTimeOffset(dateTimeOffset.Value)
            : (Instant?)null;
    }
}
