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

using Energinet.DataHub.ProcessManager.Api.Model;
using Energinet.DataHub.ProcessManager.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;

/// <summary>
/// Command for scheduling a BRS-023 or BRS-027 calculation.
/// Must be JSON serializable.
/// </summary>
public record ScheduleCalculationCommandV1
    : ScheduleOrchestrationInstanceCommand<NotifyAggregatedMeasureDataInputV1>
{
    /// <summary>
    /// Construct command.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the command.</param>
    /// <param name="runAt">The time when the orchestration instance should be executed by the Scheduler.</param>
    /// <param name="inputParameter">Contains the Durable Functions orchestration input parameter value.</param>
    public ScheduleCalculationCommandV1(
        UserIdentityDto operatingIdentity,
        DateTimeOffset runAt,
        NotifyAggregatedMeasureDataInputV1 inputParameter)
            : base(
                operatingIdentity,
                orchestrationDescriptionUniqueName: new Brs_023_027_V1(),
                runAt,
                inputParameter)
    {
    }
}