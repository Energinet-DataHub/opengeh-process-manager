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

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

/// <summary>
/// Represents the instance of a workflow (orchestration) step.
/// It contains state information about the step, and is linked
/// to the orchestration instance that it is part of.
/// </summary>
/// <param name="Id"></param>
/// <param name="Lifecycle">The high-level lifecycle states that all orchestration steps can go through.</param>
/// <param name="Description"></param>
/// <param name="Sequence">The steps number in the list of steps. The sequence of the first step in the list is 1.</param>
/// <param name="CustomState">Any custom state of the step.</param>
public record StepInstanceDto(
    Guid Id,
    StepInstanceLifecycleDto Lifecycle,
    string Description,
    int Sequence,
    string CustomState);
