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

using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Options;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Model;

/// <summary>
/// The purpose of this record is to give the orchestration information about
/// skipped steps. This allow us to handle decision about activities to skip within
/// the orchestration instead of within activities.
/// </summary>
/// <param name="OrchestrationOptions">Options for configuration of the orchestration execution.</param>
/// <param name="SkippedStepsBySequence">Contains the sequence number of any skipped steps.</param>
public record OrchestrationExecutionPlan(
    OrchestrationOptions_Brs_X01_InputExample_V1 OrchestrationOptions,
    IReadOnlyCollection<int> SkippedStepsBySequence);
