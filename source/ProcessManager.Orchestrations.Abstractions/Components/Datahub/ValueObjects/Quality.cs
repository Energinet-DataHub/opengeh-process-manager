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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;

public class Quality : DataHubType<Quality>
{
    public static readonly Quality Adjusted = new("Adjusted", "A01");           // Korrigeret
    public static readonly Quality NotAvailable = new("NotAvailable", "A02");   // Manglende værdi
    public static readonly Quality Estimated = new("Estimated", "A03");         // Estimeret
    public static readonly Quality AsProvided = new("AsProvided", "A04");       // Målt
    public static readonly Quality Incomplete = new("Incomplete", "A05");       // Ufuldstændig
    public static readonly Quality Calculated = new("Calculated", "A06");       // Beregnet

    [JsonConstructor]
    private Quality(string name, string code)
        : base(name, code)
    {
    }
}
