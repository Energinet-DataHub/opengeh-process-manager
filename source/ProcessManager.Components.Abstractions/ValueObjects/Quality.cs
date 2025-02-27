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

using System.Text.Json.Serialization;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

public record Quality : DataHubRecordType<Quality>
{
    public static readonly Quality Adjusted = new("Adjusted");           // Korrigeret
    public static readonly Quality NotAvailable = new("NotAvailable");   // Manglende værdi
    public static readonly Quality Estimated = new("Estimated");         // Estimeret
    public static readonly Quality AsProvided = new("AsProvided");       // Målt
    public static readonly Quality Incomplete = new("Incomplete");       // Ufuldstændig
    public static readonly Quality Calculated = new("Calculated");       // Beregnet

    [JsonConstructor]
    private Quality(string name)
        : base(name)
    {
    }
}
