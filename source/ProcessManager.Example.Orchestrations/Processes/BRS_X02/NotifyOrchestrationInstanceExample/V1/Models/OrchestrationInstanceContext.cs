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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Options;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Models;

/// <summary>
/// The purpose of this record is to give the orchestration information specific about orchestration specific
/// options from app settings.
/// </summary>
public record OrchestrationInstanceContext(
    OrchestrationInstanceId OrchestrationInstanceId,
    OrchestrationOptions_Brs_X02_NotifyOrchestrationInstanceExample_V1 Options);
