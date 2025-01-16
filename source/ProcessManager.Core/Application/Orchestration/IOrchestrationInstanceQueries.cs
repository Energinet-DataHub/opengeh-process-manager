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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

public interface IOrchestrationInstanceQueries
{
    /// <summary>
    /// Get existing orchestration instance.
    /// </summary>
    Task<OrchestrationInstance> GetAsync(OrchestrationInstanceId id);

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
    Task<IReadOnlyCollection<(OrchestrationDescriptionUniqueName UniqueName, OrchestrationInstance Instance)>> SearchAsync(
        IReadOnlyCollection<string> orchestrationDescriptionNames,
        Instant activatedAtOrLater,
        Instant activatedAtOrEarlier);
}
