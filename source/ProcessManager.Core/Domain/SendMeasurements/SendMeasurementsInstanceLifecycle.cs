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
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;

/// <summary>
/// An immutable lifecycle state for a BRS-021 Send Measurements instance, representing the states
/// the instance can have.
/// </summary>
public record SendMeasurementsInstanceLifecycle
{
    public SendMeasurementsInstanceLifecycle(
        Instant? terminatedAt,
        Instant? failedAt)
    {
        if (failedAt is not null)
        {
            State = OrchestrationInstanceLifecycleState.Terminated;
            TerminationState = OrchestrationInstanceTerminationState.Failed;
        }
        else if (terminatedAt is not null)
        {
            State = OrchestrationInstanceLifecycleState.Terminated;
            TerminationState = OrchestrationInstanceTerminationState.Succeeded;
        }
        else
        {
            State = OrchestrationInstanceLifecycleState.Running;
        }
    }

    public OrchestrationInstanceLifecycleState State { get; }

    public OrchestrationInstanceTerminationState? TerminationState { get; }
}
