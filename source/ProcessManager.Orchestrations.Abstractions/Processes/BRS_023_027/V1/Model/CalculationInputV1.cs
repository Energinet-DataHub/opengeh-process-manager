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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;

/// <summary>
/// An immutable input to start the orchestration instance for "BRS_023_027" V1.
/// </summary>
public record CalculationInputV1(
    CalculationTypes CalculationType,
    IReadOnlyCollection<string> GridAreaCodes,
    DateTimeOffset PeriodStartDate,
    DateTimeOffset PeriodEndDate,
    bool IsInternalCalculation)
        : IInputParameterDto;