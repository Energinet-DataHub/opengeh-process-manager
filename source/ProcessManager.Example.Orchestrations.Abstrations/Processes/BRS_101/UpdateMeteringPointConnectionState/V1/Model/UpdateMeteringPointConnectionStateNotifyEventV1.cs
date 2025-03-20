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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Model;

/// <summary>
/// Event sent from EDI to an BRS-101 Update MeteringPoint Connection State orchestration instance,
/// to notify that actor messages has been enqueued.
/// </summary>
public record UpdateMeteringPointConnectionStateNotifyEventV1(
    string OrchestrationInstanceId)
    : NotifyOrchestrationInstanceEvent(
        OrchestrationInstanceId,
        EventName: OrchestrationInstanceEventName)
{
    /// <summary>
    /// The event name which the orchestration instance expects (is waiting for).
    /// </summary>
    public const string OrchestrationInstanceEventName = "ActorMessagesEnqueued";
}
