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

using System.Text.Json.Serialization;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

public record SettlementVersion : DataHubRecordType<SettlementVersion>
{
    public static readonly SettlementVersion FirstCorrection = new("FirstCorrection");
    public static readonly SettlementVersion SecondCorrection = new("SecondCorrection");
    public static readonly SettlementVersion ThirdCorrection = new("ThirdCorrection");

    [JsonConstructor]
    private SettlementVersion(string name)
        : base(name)
    {
    }
}
