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

using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;

/// <summary>
/// Keep a register of known Durable Functions orchestrations.
/// Each orchestration is registered with information by which it is possible
/// to communicate with Durable Functions and start a new orchestration instance.
/// </summary>
internal class OrchestrationRegister(
    IOptions<ProcessManagerOptions> options,
    ILogger<OrchestrationRegister> logger,
    ProcessManagerContext context) :
        IOrchestrationRegister,
        IOrchestrationRegisterQueries
{
    private readonly ProcessManagerOptions _options = options.Value;
    private readonly ILogger<OrchestrationRegister> _logger = logger;
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
    public bool ShouldRegisterOrUpdate(OrchestrationDescription? existingDescription, OrchestrationDescription newDescription)
    {
        return
            existingDescription == null
            || existingDescription.IsEnabled == false
            || AnyRefreshablePropertyHasChanged(existingDescription, newDescription);
    }

    /// <inheritdoc />
    public async Task RegisterOrUpdateAsync(OrchestrationDescription newDescription, string hostName)
    {
        ArgumentNullException.ThrowIfNull(newDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        var existingDescription = await GetOrDefaultAsync(newDescription.UniqueName, isEnabled: null).ConfigureAwait(false);
        if (existingDescription == null)
        {
            // Enforce certain values
            newDescription.HostName = hostName;
            newDescription.IsEnabled = true;
            _context.OrchestrationDescriptions.Add(newDescription);
        }
        else
        {
            existingDescription.IsEnabled = true;
            UpdateRefreshableProperties(existingDescription, newDescription);
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

    private bool AnyRefreshablePropertyHasChanged(
        OrchestrationDescription existingDescription,
        OrchestrationDescription newDescription)
    {
        // Breaking changes for the orchestration description (should only be allowed in dev/test):
        var propertiesWithBreakingChanges = GetPropertiesWithBreakingChanges(existingDescription, newDescription);

        if (propertiesWithBreakingChanges.Any() && _options.AllowOrchestrationDescriptionBreakingChanges)
        {
            _logger.LogInformation("Updating orchestration description with breaking changes"
                + $" (Id={existingDescription.IsEnabled}, UniqueName={existingDescription.UniqueName.Name}, Version={existingDescription.UniqueName.Version},"
                + $" ChangedProperties={string.Join(", ", propertiesWithBreakingChanges)}).");
            return true;
        }
        else if (propertiesWithBreakingChanges.Any())
        {
            throw new InvalidOperationException(
                $"Breaking changes to orchestration description are not allowed"
                + $" (Id={existingDescription.IsEnabled}, UniqueName={existingDescription.UniqueName.Name}, Version={existingDescription.UniqueName.Version},"
                + $" ChangedProperties={string.Join(", ", propertiesWithBreakingChanges)}).");
        }

        return existingDescription.RecurringCronExpression != newDescription.RecurringCronExpression;
    }

    /// <summary>
    /// Get a list of properties (which are breaking changes) that have changed between the existing and new orchestration description.
    /// </summary>
    private IReadOnlyCollection<string> GetPropertiesWithBreakingChanges(
        OrchestrationDescription existingDescription,
        OrchestrationDescription newDescription)
    {
        List<string> changedProperties = [];
        if (existingDescription.Steps.Count != newDescription.Steps.Count)
        {
            changedProperties.Add(nameof(existingDescription.Steps.Count));
        }
        else
        {
            for (var stepIndex = 0; stepIndex < newDescription.Steps.Count; stepIndex++)
            {
                var newStep = newDescription.Steps.OrderBy(s => s.Sequence).ElementAt(stepIndex);
                var existingStep = newDescription.Steps.OrderBy(s => s.Sequence).ElementAtOrDefault(stepIndex);

                var stepChanged = existingStep == null
                                  || existingStep.CanBeSkipped != newStep.CanBeSkipped
                                  || existingStep.Description != newStep.Description
                                  || existingStep.SkipReason != newStep.SkipReason
                                  || existingStep.Sequence != newStep.Sequence;

                if (stepChanged)
                    changedProperties.Add($"{nameof(existingDescription.Steps)}[{stepIndex}]");
            }
        }

        if (existingDescription.FunctionName != newDescription.FunctionName)
            changedProperties.Add(nameof(existingDescription.FunctionName));

        if (existingDescription.ParameterDefinition.SerializedParameterDefinition != newDescription.ParameterDefinition.SerializedParameterDefinition)
            changedProperties.Add(nameof(existingDescription.ParameterDefinition));

        if (existingDescription.CanBeScheduled != newDescription.CanBeScheduled)
            changedProperties.Add(nameof(existingDescription.CanBeScheduled));

        return changedProperties;
    }

    /// <summary>
    /// Properties that can change the behaviour of the orchestration history should not be allowed to
    /// change without bumping the version of the orchestration description.
    /// </summary>
    private void UpdateRefreshableProperties(
        OrchestrationDescription existingDescription,
        OrchestrationDescription newDescription)
    {
        existingDescription.RecurringCronExpression = newDescription.RecurringCronExpression;

        // Breaking changes for the orchestration description (should only be allowed in dev/test):
        if (_options.AllowOrchestrationDescriptionBreakingChanges)
        {
            existingDescription.FunctionName = newDescription.FunctionName;
            existingDescription.CanBeScheduled = newDescription.CanBeScheduled;
            existingDescription.ParameterDefinition.SetSerializedParameterDefinition(
                newDescription.ParameterDefinition.SerializedParameterDefinition);

            existingDescription.OverwriteSteps(newDescription.Steps.ToList());
        }
    }
}
