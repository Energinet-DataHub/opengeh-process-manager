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

public record MeasurementUnit : DataHubRecordType<MeasurementUnit>
{
    public static readonly MeasurementUnit Ampere = new("Ampere");
    public static readonly MeasurementUnit Pieces = new("Pieces");
    public static readonly MeasurementUnit KiloVoltAmpereReactiveHour = new("KiloVoltAmpereReactiveHour");
    public static readonly MeasurementUnit KilowattHour = new("KilowattHour");
    public static readonly MeasurementUnit Kilowatt = new("Kilowatt");
    public static readonly MeasurementUnit Megawatt = new("Megawatt");
    public static readonly MeasurementUnit MegawattHour = new("MegawattHour");
    public static readonly MeasurementUnit MetricTon = new("MetricTon");
    public static readonly MeasurementUnit MegaVoltAmpereReactivePower = new("MegaVoltAmpereReactivePower");
    public static readonly MeasurementUnit DanishTariffCode = new("Kw");

    [JsonConstructor]
    private MeasurementUnit(string name)
        : base(name)
    {
    }
}
