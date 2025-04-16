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

using System.Globalization;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Extensions;

public class InstantPatternWithOptionalSeconds
{
    /// <summary>
    /// Supported formats:
    /// Cim formats
    /// yyyy-MM-ddTHH:mm'Z'
    /// Ebix formats
    /// yyyy-MM-ddTHH:mm:ss'Z'
    /// yyyy-MM-ddTHH:mm:ss.fffffff'Z'
    /// </summary>
    /// <param name="dateTime"></param>
    public static ParseResult<Instant> Parse(string dateTime)
    {
        var pattern = new CompositePatternBuilder<Instant>
            {
                // allowed to excl seconds
                { InstantPattern.Create("yyyy-MM-ddTHH:mm'Z'", CultureInfo.InvariantCulture), _ => false },
                // allowed to incl seconds
                { InstantPattern.Create("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture), _ => false },
                // allowed to incl seconds and fractional seconds
                { InstantPattern.Create("yyyy-MM-ddTHH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture), _ => false },
            }
            .Build();
        return pattern.Parse(dateTime);
    }
}
