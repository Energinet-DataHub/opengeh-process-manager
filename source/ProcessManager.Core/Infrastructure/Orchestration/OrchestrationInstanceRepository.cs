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

using System.Linq;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using NodaTime;

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

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OrchestrationInstance>> SearchAsync(
        string name,
        int? version = default,
        OrchestrationInstanceLifecycleState? lifecycleState = default,
        OrchestrationInstanceTerminationState? terminationState = default,
        Instant? startedAtOrLater = default,
        Instant? terminatedAtOrEarlier = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Temp for testing
        var searchParams = new TestOrchestrationParameter
        {
            IsInternalCalculation = false,
            PeriodStartDate = DateTime.Now,
            PeriodEndDate = DateTime.Now.AddDays(2),
            CalculationTypes = ["1"],
            GridAreaCodes = ["804"],
        };

        // Query OrchestrationInstances
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

        // Query ParameterValue JSON string
        var actualOrchestrationInstanceIds = _context.Database
            .SqlQuery<TestOrchestrationParameter>($"""
        SELECT
            CAST(JSON_VALUE([o].[SerializedParameterValue], '$.PeriodStartDate') AS datetimeoffset) AS PeriodStartDate,
            CAST(JSON_VALUE([o].[SerializedParameterValue], '$.PeriodEndDate') AS datetimeoffset) AS PeriodEndDate,
            CAST(JSON_VALUE([o].[SerializedParameterValue], '$.IsInternalCalculation') AS bit) AS IsInternalCalculation,
            JSON_QUERY([o].[SerializedParameterValue], '$.CalculationTypes') AS CalculationTypes,
            JSON_QUERY([o].[SerializedParameterValue], '$.GridAreaCodes') AS GridAreaCodes,
            o.Id AS OrchestrationInstanceId
        FROM
            [pm].[OrchestrationInstance] AS [o]
        """)
            .GroupBy(x => new
            {
                x.CalculationTypes,
                x.GridAreaCodes,
                x.PeriodStartDate,
                x.PeriodEndDate,
                x.IsInternalCalculation,
                x.OrchestrationInstanceId,
            })
            .Select(x => new TestOrchestrationParameter
            {
                CalculationTypes = x.Key.CalculationTypes,
                GridAreaCodes = x.Key.GridAreaCodes,
                PeriodStartDate = x.Key.PeriodStartDate,
                PeriodEndDate = x.Key.PeriodEndDate,
                IsInternalCalculation = x.Key.IsInternalCalculation,
                OrchestrationInstanceId = x.Key.OrchestrationInstanceId,
            })
            .Where(x =>
                (searchParams.IsInternalCalculation == null || x.IsInternalCalculation == searchParams.IsInternalCalculation) &&
                (searchParams.PeriodStartDate == null || x.PeriodStartDate >= searchParams.PeriodStartDate) &&
                (searchParams.PeriodEndDate == null || x.PeriodEndDate <= searchParams.PeriodEndDate) &&
                (searchParams.CalculationTypes == null || searchParams.CalculationTypes.Any(filter => x.CalculationTypes!.Contains(filter))) &&
                (searchParams.GridAreaCodes == null || searchParams.GridAreaCodes.Any(filter => x.GridAreaCodes!.Contains(filter))));

        // Join OrchestrationInstances with matching ParameterValues.
        var combinedResults = query.Join(
            actualOrchestrationInstanceIds,
            instance => instance.Id,
            param => new OrchestrationInstanceId(param.OrchestrationInstanceId),
            (instance, param) => instance);

        return await combinedResults.ToListAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<(OrchestrationDescriptionUniqueName UniqueName, OrchestrationInstance Instance)>> SearchAsync(
        IReadOnlyCollection<string> orchestrationDescriptionNames,
        Instant activatedAtOrLater,
        Instant activatedAtOrEarlier)
    {
        var query = _context
            .OrchestrationDescriptions
                .Where(x => orchestrationDescriptionNames.Contains(x.UniqueName.Name))
            .Join(
                _context.OrchestrationInstances,
                description => description.Id,
                instance => instance.OrchestrationDescriptionId,
                (description, instance) => new { description.UniqueName, instance })
            .Where(x =>
                (x.instance.Lifecycle.QueuedAt >= activatedAtOrLater && x.instance.Lifecycle.QueuedAt <= activatedAtOrEarlier)
                || (x.instance.Lifecycle.ScheduledToRunAt >= activatedAtOrLater && x.instance.Lifecycle.ScheduledToRunAt <= activatedAtOrEarlier))
            .Select(x => ValueTuple.Create(x.UniqueName, x.instance));

        return await query.ToListAsync().ConfigureAwait(false);
    }

    private class TestOrchestrationParameter
    {
        public Guid OrchestrationInstanceId { get; set; }

        public IList<string?>? CalculationTypes { get; set; }

        public IList<string?>? GridAreaCodes { get; set; }

        public DateTimeOffset? PeriodStartDate { get; set; }

        public DateTimeOffset? PeriodEndDate { get; set; }

        public bool? IsInternalCalculation { get; set; }
    }
}
