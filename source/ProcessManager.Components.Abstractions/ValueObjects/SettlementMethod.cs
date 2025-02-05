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

namespace Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

public record SettlementMethod : DataHubRecordType<SettlementMethod>
{
    // Customer with more than ~100.000 kwH per year
    public static readonly SettlementMethod NonProfiled = new("NonProfiled");

    // Customer with less than ~100.000 kwH per year
    public static readonly SettlementMethod Flex = new("Flex");

    [JsonConstructor]
    private SettlementMethod(string name)
        : base(name)
    {
    }
}
