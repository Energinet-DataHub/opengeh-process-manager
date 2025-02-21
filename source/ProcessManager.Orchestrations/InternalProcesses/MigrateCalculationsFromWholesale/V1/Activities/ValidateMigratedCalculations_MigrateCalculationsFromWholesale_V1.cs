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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.InternalProcesses.MigrateCalculationsFromWholesale;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrchestrationInstanceLifecycleState = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance.OrchestrationInstanceLifecycleState;
using OrchestrationInstanceTerminationState = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance.OrchestrationInstanceTerminationState;
using OrchestrationStepTerminationState = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance.OrchestrationStepTerminationState;
using StepInstanceLifecycleState = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance.StepInstanceLifecycleState;

namespace Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;

public class ValidateMigratedCalculations_MigrateCalculationsFromWholesale_V1(
    ILogger<ValidateMigratedCalculations_MigrateCalculationsFromWholesale_V1> logger,
    WholesaleContext wholesaleContext,
    ProcessManagerContext processManagerContext,
    IOrchestrationInstanceQueries orchestrationInstanceQueries)
{
    private readonly ILogger<ValidateMigratedCalculations_MigrateCalculationsFromWholesale_V1> _logger = logger;
    private readonly WholesaleContext _wholesaleContext = wholesaleContext;
    private readonly ProcessManagerContext _processManagerContext = processManagerContext;
    private readonly IOrchestrationInstanceQueries _orchestrationInstanceQueries = orchestrationInstanceQueries;

    [Function(nameof(ValidateMigratedCalculations_MigrateCalculationsFromWholesale_V1))]
    public async Task Run(
        [ActivityTrigger] FunctionContext functionContext)
    {
        var wholesaleCalculations = await _wholesaleContext.Calculations
            .AsNoTracking()
            .Where(c => c.OrchestrationState == CalculationOrchestrationState.Completed)
            .ToListAsync()
            .ConfigureAwait(false);

        var migratedCalculations = await _processManagerContext.OrchestrationDescriptions
            .AsNoTracking()
            .Where(od =>
                od.UniqueName.Name == MigrateCalculationsFromWholesaleUniqueName.V1.Name
                && od.UniqueName.Version == MigrateCalculationsFromWholesaleUniqueName.V1.Version)
            .Join(
                inner: _processManagerContext.OrchestrationInstances,
                outerKeySelector: od => od.Id,
                innerKeySelector: oi => oi.OrchestrationDescriptionId,
                resultSelector: (od, oi) => oi)
            .AsNoTracking()
            .Where(oi => oi.CustomState.Value.Contains(MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.MigratedWholesaleCalculationIdCustomStatePrefix))
            .ToListAsync()
            .ConfigureAwait(false);

        var notMigratedWholesaleCalculations = GetNotMigratedWholesaleCalculations(
            wholesaleCalculations,
            migratedCalculations);

        var migrationErrors = await GetIncorrectlyMigratedCalculations(migratedCalculations).ConfigureAwait(false);

        foreach (var notMigratedWholesaleCalculation in notMigratedWholesaleCalculations)
        {
            migrationErrors.Add(notMigratedWholesaleCalculation.Id, ["Not migrated"]);
        }

        if (migrationErrors.Count != 0)
        {
            _logger.LogError(
                "Errors while migrating Wholesale calculations. Failed calculation ids: {FailedCalculationIds}, migration errors: {MigrationErrors}",
                migrationErrors.Select(e => e.Key).ToList(),
                migrationErrors);
            throw new Exception("Errors while migrating Wholesale calculations (Failed calculation ids: " + string.Join(", ", migrationErrors.Keys) + ")")
            {
                Data =
                {
                    { "MigrationErrors", migrationErrors },
                },
            };
        }
    }

    private IReadOnlyCollection<Calculation> GetNotMigratedWholesaleCalculations(
        List<Calculation> wholesaleCalculations,
        List<OrchestrationInstance> migratedCalculations)
    {
        var migratedCalculationIds = migratedCalculations
            .Select(oi => oi.CustomState)
            .Select(MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.GetMigratedWholesaleCalculationIdCustomStateGuid)
            .ToList();

        var notMigratedCalculations = wholesaleCalculations
            .Where(c => !migratedCalculationIds.Contains(c.Id))
            .ToList();

        return notMigratedCalculations;
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<string>>> GetIncorrectlyMigratedCalculations(List<OrchestrationInstance> allMigratedCalculations)
    {
        var migrationErrorsTasks = allMigratedCalculations
            .Select(GetMigrationErrorsForCalculation)
            .ToList();

        var migrationErrors = await Task.WhenAll(migrationErrorsTasks)
            .ConfigureAwait(false);

        return migrationErrors
            .Where(c => c.Value.Count > 0)
            .ToDictionary();
    }

    private async Task<KeyValuePair<Guid, IReadOnlyCollection<string>>> GetMigrationErrorsForCalculation(OrchestrationInstance migratedCalculation)
    {
        var wholesaleCalculationId = MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1
            .GetMigratedWholesaleCalculationIdCustomStateGuid(migratedCalculation.CustomState);

        var orchestrationInstance = await _orchestrationInstanceQueries
            .GetAsync(migratedCalculation.Id)
            .ConfigureAwait(false);

        var asTypedDto = orchestrationInstance.MapToTypedDto<CalculationInputV1>();

        // Verify that the typed ParameterValue works and the at least one GridAreaCodes is present.
        var checks = new Dictionary<string, bool>
        {
            {
                nameof(asTypedDto.Lifecycle.State),
                asTypedDto.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
            },
            {
                nameof(asTypedDto.Lifecycle.TerminationState),
                asTypedDto.Lifecycle.TerminationState == OrchestrationInstanceTerminationState.Succeeded
            },
            {
                nameof(asTypedDto.Lifecycle.StartedAt),
                asTypedDto.Lifecycle.StartedAt != null
                && asTypedDto.Lifecycle.StartedAt != default(DateTimeOffset)
            },
            {
                nameof(asTypedDto.Lifecycle.TerminatedAt),
                asTypedDto.Lifecycle.TerminatedAt != null
                && asTypedDto.Lifecycle.TerminatedAt != default(DateTimeOffset)
            },
            {
                nameof(asTypedDto.ParameterValue.GridAreaCodes),
                asTypedDto.ParameterValue.GridAreaCodes.Count > 0
            },
            {
                nameof(asTypedDto.ParameterValue.CalculationType),
                Enum.IsDefined(asTypedDto.ParameterValue.CalculationType)
            },
            {
                nameof(asTypedDto.ParameterValue.PeriodStartDate),
                asTypedDto.ParameterValue.PeriodStartDate != default
            },
            {
                nameof(asTypedDto.ParameterValue.PeriodEndDate),
                asTypedDto.ParameterValue.PeriodStartDate != default
            },
        };

        var stepChecks = asTypedDto.Steps
            .SelectMany(
                s =>
                {
                    var stepName = $"Step {s.Sequence}";
                    return new Dictionary<string, bool>
                    {
                        {
                            $"{stepName}: {nameof(s.Lifecycle.State)}",
                            s.Lifecycle.State != StepInstanceLifecycleState.Terminated
                        },
                        {
                            $"{stepName}: {nameof(s.Lifecycle.TerminationState)}",
                            s.Lifecycle.TerminationState == OrchestrationStepTerminationState.Succeeded
                        },
                        {
                            $"{stepName}: {nameof(s.Lifecycle.StartedAt)}",
                            s.Lifecycle.StartedAt != null
                            && s.Lifecycle.StartedAt != default(DateTimeOffset)
                        },
                        {
                            $"{stepName}: {nameof(s.Lifecycle.TerminatedAt)}",
                            s.Lifecycle.TerminatedAt != null
                            && s.Lifecycle.TerminatedAt != default(DateTimeOffset)
                        },
                    };
                });

        checks = checks.Concat(stepChecks).ToDictionary();

        var invalidChecks = checks
            .Where(c => c.Value == false)
            .Select(c => c.Key)
            .ToList();

        return new KeyValuePair<Guid, IReadOnlyCollection<string>>(wholesaleCalculationId, invalidChecks);
    }
}
