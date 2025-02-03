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

[Serializable]
public class Resolution : DataHubType<Resolution>
{
    public static readonly Resolution QuarterHourly = new("QuarterHourly", "PT15M");
    public static readonly Resolution Hourly = new("Hourly", "PT1H");
    public static readonly Resolution Daily = new("Daily", "P1D");
    public static readonly Resolution Monthly = new("Monthly", "P1M");

    [JsonConstructor]
    private Resolution(string name, string code)
        : base(name, code)
    {
    }
}
