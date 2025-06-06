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

using Energinet.DataHub.Measurements.Contracts;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Mappers;

public static class DecimalValueMapper
{
    public static DecimalValue Map(decimal value)
    {
        var units = decimal.ToInt64(value);
        var nanoFactor = 1_000_000_000;
        return new DecimalValue() { Units = units, Nanos = decimal.ToInt32((value - units) * nanoFactor), };
    }
}
