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
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using OrchestrationInstanceLifecycleState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceLifecycleState;
using OrchestrationInstanceTerminationState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceTerminationState;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;

/// <summary>
/// Read/write access to the orchestration instance repository.
/// </summary>
internal class OrchestrationInstanceRepository(
    ProcessManagerContext context) :
        IOrchestrationInstanceRepository,
        IOrchestrationInstanceProgressRepository,
        IOrchestrationInstanceQueries,
        IScheduledOrchestrationInstancesByInstantQuery
{
    private readonly ProcessManagerContext _context = context;

    /// <inheritdoc />
    public IUnitOfWork UnitOfWork => _context;

    /// <inheritdoc />
    public Task<OrchestrationInstance> GetAsync(OrchestrationInstanceId id)
    {
        return _context.OrchestrationInstances.FirstAsync(x => x.Id == id);
    }

    /// <inheritdoc />
    public Task<OrchestrationInstance?> GetOrDefaultAsync(OrchestrationInstanceId id)
    {
        return _context.OrchestrationInstances.FirstOrDefaultAsync(x => x.Id == id);
    }

    /// <inheritdoc />
    public Task<OrchestrationInstance?> GetOrDefaultAsync(IdempotencyKey idempotencyKey)
    {
        return _context.OrchestrationInstances.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);
    }

    /// <inheritdoc />
    public async Task AddAsync(OrchestrationInstance orchestrationInstance)
    {
        await _context.OrchestrationInstances.AddAsync(orchestrationInstance).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IScheduledOrchestrationInstancesByInstantQuery.FindAsync(Instant)"/>
    public async Task<IReadOnlyCollection<OrchestrationInstance>> FindAsync(Instant scheduledToRunBefore)
    {
        var query = _context.OrchestrationInstances
            .Where(x => x.Lifecycle.State == OrchestrationInstanceLifecycleState.Pending)
            .Where(x => x.Lifecycle.ScheduledToRunAt != null && x.Lifecycle.ScheduledToRunAt.Value <= scheduledToRunBefore);

        return await query.ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Get all orchestration instances filtered by their related orchestration definition name and version,
    /// and their lifecycle / termination states.
    /// <remarks>
    /// The SearchAsync queries on this repository is covered by the "IX_OrchestrationInstance_SearchComposite" index,
    /// so if one of the queries are updated then the index should be updated as well.
    /// </remarks>
    /// </summary>
    public async Task<IReadOnlyCollection<OrchestrationInstance>> SearchAsync(
        string name,
        int? version = default,
        OrchestrationInstanceLifecycleState? lifecycleState = default,
        OrchestrationInstanceTerminationState? terminationState = default,
        Instant? startedAtOrLater = default,
        Instant? terminatedAtOrEarlier = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var query = _context
            .OrchestrationDescriptions
                .Where(x => x.UniqueName.Name == name)
                .Where(x => version == null || x.UniqueName.Version == version)
            .Join(
                _context.OrchestrationInstances,
                description => description.Id,
                instance => instance.OrchestrationDescriptionId,
                (_, instance) => instance)
            .Where(x => lifecycleState == null || x.Lifecycle.State == lifecycleState)
            .Where(x => terminationState == null || x.Lifecycle.TerminationState == terminationState)
            .Where(x => startedAtOrLater == null || x.Lifecycle.StartedAt >= startedAtOrLater)
            .Where(x => terminatedAtOrEarlier == null || x.Lifecycle.TerminatedAt <= terminatedAtOrEarlier);

        return await query.ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Get all orchestration instances filtered by their orchestration description name and activation
    /// (queued at/scheduled to run at) timestamp. This means orchestration instances from different
    /// orchestration descriptions can be searched for and returned.
    /// Use the returned unique name to determine which orchestration description a given orchestration instance
    /// was created from.
    /// <remarks>
    /// The SearchAsync queries on this repository is covered by the "IX_OrchestrationInstance_SearchComposite" index,
    /// so if one of the queries are updated then the index should be updated as well.
    /// </remarks>
    /// </summary>
    /// <param name="orchestrationDescriptionNames"></param>
    /// <param name="activatedAtOrLater"></param>
    /// <param name="activatedAtOrEarlier"></param>
    /// <param name="createdByActorNumber">Filter by the actor number that created the orchestration instance. If not provided then all will be returned.</param>
    /// <param name="createdByActorRole">Filter by the actor role that created the orchestration instance. If not provided then all will be returned.</param>
    public async Task<IReadOnlyCollection<(OrchestrationDescriptionUniqueName UniqueName, OrchestrationInstance Instance)>> SearchAsync(
        IReadOnlyCollection<string> orchestrationDescriptionNames,
        Instant activatedAtOrLater,
        Instant activatedAtOrEarlier,
        ActorNumber? createdByActorNumber,
        ActorRole? createdByActorRole)
    {
        var query = _context
            .OrchestrationDescriptions
                .Where(x => orchestrationDescriptionNames.Contains(x.UniqueName.Name))
            .Join(
                _context.OrchestrationInstances,
                description => description.Id,
                instance => instance.OrchestrationDescriptionId,
                (description, instance) => new { description.UniqueName, instance })
            .Where(x => createdByActorNumber == null || x.instance.Lifecycle.CreatedBy.ActorNumber == createdByActorNumber)
            .Where(x => createdByActorRole == null || x.instance.Lifecycle.CreatedBy.ActorRole == createdByActorRole)
            .Where(x =>
                (x.instance.Lifecycle.QueuedAt >= activatedAtOrLater && x.instance.Lifecycle.QueuedAt <= activatedAtOrEarlier)
                || (x.instance.Lifecycle.ScheduledToRunAt >= activatedAtOrLater && x.instance.Lifecycle.ScheduledToRunAt <= activatedAtOrEarlier))
            .Select(x => ValueTuple.Create(x.UniqueName, x.instance));

        return await query.ToListAsync().ConfigureAwait(false);
    }
}
