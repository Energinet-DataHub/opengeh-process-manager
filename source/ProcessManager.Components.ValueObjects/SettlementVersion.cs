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

namespace Energinet.DataHub.ProcessManager.Components.ValueObjects;

public class SettlementVersion : DataHubType<SettlementVersion>
{
    public static readonly SettlementVersion FirstCorrection = new("FirstCorrection", "D01");
    public static readonly SettlementVersion SecondCorrection = new("SecondCorrection", "D02");
    public static readonly SettlementVersion ThirdCorrection = new("ThirdCorrection", "D03");

    [JsonConstructor]
    private SettlementVersion(string name, string code)
        : base(name, code)
    {
    }
}
