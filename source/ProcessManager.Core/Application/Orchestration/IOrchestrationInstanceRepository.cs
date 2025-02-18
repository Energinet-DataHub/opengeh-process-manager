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

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

internal interface IOrchestrationInstanceRepository
{
    /// <summary>
    /// Use <see cref="IUnitOfWork.CommitAsync"/> to save changes.
    /// </summary>
    public IUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// Add the orchestration instance.
    /// To commit changes use <see cref="UnitOfWork"/>.
    /// </summary>
    Task AddAsync(OrchestrationInstance orchestrationInstance);

    /// <summary>
    /// Get existing orchestration instance by id.
    /// To commit changes use <see cref="UnitOfWork"/>.
    /// </summary>
    Task<OrchestrationInstance> GetAsync(OrchestrationInstanceId id);

    /// <summary>
    /// Get existing orchestration instance by id, if it exists.
    /// To commit changes use <see cref="UnitOfWork"/>.
    /// </summary>
    Task<OrchestrationInstance?> GetOrDefaultAsync(OrchestrationInstanceId id);

    /// <summary>
    /// Get existing orchestration instance by idempotency key.
    /// To commit changes use <see cref="UnitOfWork"/>.
    /// </summary>
    Task<OrchestrationInstance?> GetOrDefaultAsync(IdempotencyKey idempotencyKey);

    /// <summary>
    /// Get all orchestration instances filtered by their related orchestration definition name
    /// and version, and their lifecycle / termination states.
    /// </summary>
    Task<IReadOnlyCollection<OrchestrationInstance>> SearchAsync(
        string name,
        int? version,
        IReadOnlyCollection<OrchestrationInstanceLifecycleState?>? lifecycleState,
        OrchestrationInstanceTerminationState? terminationState,
        Instant? startedAtOrLater,
        Instant? terminatedAtOrEarlier,
        Instant? scheduledAtOrLater);
}
