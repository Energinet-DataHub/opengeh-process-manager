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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
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
    ProcessManagerContext processManagerContext)
{
    private readonly ILogger<ValidateMigratedCalculations_MigrateCalculationsFromWholesale_V1> _logger = logger;
    private readonly WholesaleContext _wholesaleContext = wholesaleContext;
    private readonly ProcessManagerContext _processManagerContext = processManagerContext;

    [Function(nameof(ValidateMigratedCalculations_MigrateCalculationsFromWholesale_V1))]
    public async Task<string> Run(
        [ActivityTrigger] FunctionContext functionContext)
    {
        var wholesaleCalculations = await _wholesaleContext
            .CreateCalculationsToMigrateQuery()
            .ToListAsync()
            .ConfigureAwait(false);

        var migratedCalculations = await _processManagerContext
            .CreateMigratedCalculationsQuery()
            .ToListAsync()
            .ConfigureAwait(false);

        var notMigratedWholesaleCalculations = GetNotMigratedWholesaleCalculations(
            wholesaleCalculations,
            migratedCalculations);

        var migrationErrors = GetIncorrectlyMigratedCalculations(migratedCalculations);

        foreach (var notMigratedWholesaleCalculation in notMigratedWholesaleCalculations)
        {
            migrationErrors.Add(notMigratedWholesaleCalculation.Id, ["Not migrated"]);
        }

        if (migrationErrors.Count != 0)
        {
            _logger.LogError(
                "Errors while migrating Wholesale calculations. Failed calculations count: {FailedCalculationsCount}, migration errors: {MigrationErrors}",
                migrationErrors.Count,
                migrationErrors);
            throw new Exception("Errors while migrating Wholesale calculations. Failed calculations: " + string.Join("\n", migrationErrors.Select(e => $"{e.Key}: [{string.Join(", ", e.Value)}]")))
            {
                Data =
                {
                    { "MigrationErrors", migrationErrors },
                },
            };
        }

        return $"Validated {migratedCalculations.Count} migrated calculations.";
    }

    private IReadOnlyCollection<Calculation> GetNotMigratedWholesaleCalculations(
        IReadOnlyCollection<Calculation> wholesaleCalculations,
        IReadOnlyCollection<OrchestrationInstance> migratedCalculations)
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

    private Dictionary<Guid, IReadOnlyCollection<string>> GetIncorrectlyMigratedCalculations(IReadOnlyCollection<OrchestrationInstance> allMigratedCalculations)
    {
        var migrationErrors = allMigratedCalculations
            .Select(GetMigrationErrorsForCalculation)
            .ToList();

        return migrationErrors
            .Where(c => c.Value.Count > 0)
            .ToDictionary();
    }

    private KeyValuePair<Guid, IReadOnlyCollection<string>> GetMigrationErrorsForCalculation(OrchestrationInstance migratedCalculation)
    {
        var wholesaleCalculationId = MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1
            .GetMigratedWholesaleCalculationIdCustomStateGuid(migratedCalculation.CustomState);

        var asTypedDto = migratedCalculation.MapToTypedDto<CalculationInputV1>();

        // Verify that the orchestration instance is mapped to a typed DTO correctly.
        var checks = new Dictionary<string, bool>
        {
            {
                nameof(asTypedDto.Lifecycle.State),
                asTypedDto.Lifecycle.State is OrchestrationInstanceLifecycleState.Terminated
            },
            {
                nameof(asTypedDto.Lifecycle.TerminationState),
                asTypedDto.Lifecycle.TerminationState is OrchestrationInstanceTerminationState.Succeeded
            },
            {
                nameof(asTypedDto.Lifecycle.StartedAt),
                asTypedDto.Lifecycle.StartedAt is not null
                && asTypedDto.Lifecycle.StartedAt != default(DateTimeOffset)
            },
            {
                nameof(asTypedDto.Lifecycle.TerminatedAt),
                asTypedDto.Lifecycle.TerminatedAt is not null
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
                asTypedDto.ParameterValue.PeriodEndDate != default
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
                            s.Lifecycle.State is StepInstanceLifecycleState.Terminated
                        },
                        {
                            $"{stepName}: {nameof(s.Lifecycle.TerminationState)}",
                            s.Lifecycle.TerminationState is OrchestrationStepTerminationState.Skipped or OrchestrationStepTerminationState.Succeeded
                        },
                        {
                            $"{stepName}: {nameof(s.Lifecycle.StartedAt)}",
                            s.Lifecycle.TerminationState is OrchestrationStepTerminationState.Skipped || (
                                s.Lifecycle.StartedAt != null
                                && s.Lifecycle.StartedAt != default(DateTimeOffset))
                        },
                        {
                            $"{stepName}: {nameof(s.Lifecycle.TerminatedAt)}",
                            s.Lifecycle.TerminatedAt is not null
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
