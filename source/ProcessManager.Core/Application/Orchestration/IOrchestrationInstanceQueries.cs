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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using NodaTime;
using OrchestrationInstanceLifecycleState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceLifecycleState;
using OrchestrationInstanceTerminationState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceTerminationState;

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

public interface IOrchestrationInstanceQueries
{
    /// <summary>
    /// Get existing orchestration instance by id.
    /// </summary>
    Task<OrchestrationInstance> GetAsync(OrchestrationInstanceId id);

    /// <summary>
    /// Get existing orchestration instance by idempotency key.
    /// </summary>
    Task<OrchestrationInstance?> GetOrDefaultAsync(IdempotencyKey idempotencyKey);

    /// <summary>
    /// Get all orchestration instances filtered by their related orchestration definition name and version,
    /// and their lifecycle / termination states.
    /// </summary>
    Task<IReadOnlyCollection<OrchestrationInstance>> SearchAsync(
        string name,
        int? version,
        OrchestrationInstanceLifecycleState? lifecycleState,
        OrchestrationInstanceTerminationState? terminationState,
        Instant? startedAtOrLater,
        Instant? terminatedAtOrEarlier);

    /// <summary>
    /// Get all orchestration instances filtered by their orchestration description name and activation
    /// (queued at/scheduled to run at) timestamp. This means orchestration instances from different
    /// orchestration descriptions can be searched for and returned.
    /// Use the returned unique name to determine which orchestration description a given orchestration instance
    /// was created from.
    /// </summary>
    /// <param name="orchestrationDescriptionNames"></param>
    /// <param name="activatedAtOrLater"></param>
    /// <param name="activatedAtOrEarlier"></param>
    /// <param name="createdByActorNumber">Filter by the actor number that created the orchestration instance. If not provided then all will be returned.</param>
    /// <param name="createdByActorRole">Filter by the actor role that created the orchestration instance. If not provided then all will be returned.</param>
    Task<IReadOnlyCollection<(OrchestrationDescriptionUniqueName UniqueName, OrchestrationInstance Instance)>> SearchAsync(
        IReadOnlyCollection<string> orchestrationDescriptionNames,
        Instant activatedAtOrLater,
        Instant activatedAtOrEarlier,
        ActorNumber? createdByActorNumber,
        ActorRole? createdByActorRole);
}
