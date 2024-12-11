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

using Energinet.DataHub.ProcessManagement.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManagement.Core.Infrastructure.Scheduling;

/// <summary>
/// Queries necessary to handle the planning of recurring orchestrations.
/// </summary>
internal class RecurringOrchestrationQueries(
        ProcessManagerContext context) :
            IRecurringOrchestrationQueries
{
    private readonly ProcessManagerContext _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OrchestrationDescription>> SearchRecurringOrchestrationDescriptionsAsync()
    {
        var query = _context.OrchestrationDescriptions
            .Where(x => x.IsEnabled == true)
            .Where(x => x.RecurringCronExpression != string.Empty);

        return await query.ToListAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OrchestrationInstance>> SearchScheduledOrchestrationInstancesAsync(
        OrchestrationDescriptionUniqueName uniqueName,
        Instant runAtOrLater,
        Instant runAtOrEarlier)
    {
        var query = _context
            .OrchestrationDescriptions
                .Where(x => x.UniqueName == uniqueName)
            .Join(
                _context.OrchestrationInstances,
                description => description.Id,
                instance => instance.OrchestrationDescriptionId,
                (_, instance) => instance)
            .Where(x => x.Lifecycle.ScheduledToRunAt >= runAtOrLater)
            .Where(x => x.Lifecycle.ScheduledToRunAt <= runAtOrEarlier);

        return await query.ToListAsync().ConfigureAwait(false);
    }
}
