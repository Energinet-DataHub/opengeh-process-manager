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

using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Application.Registration;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManagement.Core.Infrastructure.Registration;

/// <summary>
/// Keep a register of known Durable Functions orchestrations.
/// Each orchestration is registered with information by which it is possible
/// to communicate with Durable Functions and start a new orchestration instance.
/// </summary>
internal class OrchestrationRegister(
    ProcessManagerContext context) :
        IOrchestrationRegister,
        IOrchestrationRegisterQueries
{
    private readonly ProcessManagerContext _context = context;

    /// <inheritdoc />
    public Task<OrchestrationDescription> GetAsync(OrchestrationDescriptionId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _context.OrchestrationDescriptions.FirstAsync(x => x.Id == id);
    }

    /// <inheritdoc />
    public Task<OrchestrationDescription?> GetOrDefaultAsync(OrchestrationDescriptionUniqueName uniqueName, bool? isEnabled)
    {
        return _context.OrchestrationDescriptions
            .SingleOrDefaultAsync(x =>
                x.UniqueName == uniqueName
                && (isEnabled == null || x.IsEnabled == isEnabled));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OrchestrationDescription>> GetAllByHostNameAsync(string hostName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        var query = _context.OrchestrationDescriptions
            .Where(x => x.HostName == hostName);

        return await query.ToListAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool ShouldRegisterOrUpdate(OrchestrationDescription? registerDescription, OrchestrationDescription hostDescription)
    {
        return
            registerDescription == null
            || registerDescription.IsEnabled == false
            || AnyRefreshablePropertyHasChanged(registerDescription, hostDescription);
    }

    /// <inheritdoc />
    public async Task RegisterOrUpdateAsync(OrchestrationDescription hostDescription, string hostName)
    {
        ArgumentNullException.ThrowIfNull(hostDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        var existingDescription = await GetOrDefaultAsync(hostDescription.UniqueName, isEnabled: null).ConfigureAwait(false);
        if (existingDescription == null)
        {
            // Enforce certain values
            hostDescription.HostName = hostName;
            hostDescription.IsEnabled = true;
            _context.Add(hostDescription);
        }
        else
        {
            existingDescription.IsEnabled = true;
            UpdateRefreshableProperties(existingDescription, hostDescription);
        }

        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeregisterAsync(OrchestrationDescription registerDescription)
    {
        ArgumentNullException.ThrowIfNull(registerDescription);

        var existingDescription = await GetOrDefaultAsync(registerDescription.UniqueName, isEnabled: true).ConfigureAwait(false);
        if (existingDescription == null)
            throw new InvalidOperationException("Orchestration description has not been registered or is not currently enabled.");

        existingDescription.IsEnabled = false;

        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static bool AnyRefreshablePropertyHasChanged(
        OrchestrationDescription registerDescription,
        OrchestrationDescription hostDescription)
    {
        return registerDescription.RecurringCronExpression != hostDescription.RecurringCronExpression;
    }

    /// <summary>
    /// Properties that can change the behaviour of the orchestation history should not be allowed to
    /// change without bumping the version of the orchestration description.
    /// </summary>
    private static void UpdateRefreshableProperties(
        OrchestrationDescription registerDescription,
        OrchestrationDescription hostDescription)
    {
        registerDescription.RecurringCronExpression = hostDescription.RecurringCronExpression;
    }
}
