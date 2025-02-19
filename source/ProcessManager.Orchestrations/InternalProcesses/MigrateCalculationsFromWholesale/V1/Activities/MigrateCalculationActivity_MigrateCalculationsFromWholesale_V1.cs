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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Steps;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;

public class MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1(
    WholesaleContext wholesaleContext,
    ProcessManagerContext processManagerContext,
    IOrchestrationInstanceFactory orchestrationInstanceFactory)
{
    public const string MigratedWholesaleCalculationIdCustomStatePrefix = "MigratedWholesaleCalculationId=";

    private readonly WholesaleContext _wholesaleContext = wholesaleContext;
    private readonly ProcessManagerContext _processManagerContext = processManagerContext;
    private readonly IOrchestrationInstanceFactory _orchestrationInstanceFactory = orchestrationInstanceFactory;

    public static OrchestrationInstanceCustomState GetMigratedWholesaleCalculationIdCustomState(Guid wholesaleCalculationId)
    {
        return new OrchestrationInstanceCustomState { Value = $"{MigratedWholesaleCalculationIdCustomStatePrefix}{wholesaleCalculationId}" };
    }

    public static Guid GetMigratedWholesaleCalculationIdCustomStateGuid(OrchestrationInstanceCustomState customState)
    {
        var calculationIdString = customState.Value.Replace(MigratedWholesaleCalculationIdCustomStatePrefix, string.Empty);
        var calculationId = Guid.Parse(calculationIdString);
        return calculationId;
    }

    [Function(nameof(MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1))]
    public async Task<string> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var migratedCalculation = await _processManagerContext.OrchestrationInstances
            .AsNoTracking()
            .Where(x => x.CustomState == GetMigratedWholesaleCalculationIdCustomState(input.CalculationToMigrateId))
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (migratedCalculation == null)
        {
            var wholesaleCalculation = await _wholesaleContext.Calculations
                .FirstAsync(x => x.Id == input.CalculationToMigrateId)
                .ConfigureAwait(false);

            // Created + Queued
            var orchestrationInstance = await CreateQueuedOrchestrationInstanceAsync(wholesaleCalculation).ConfigureAwait(false);

            orchestrationInstance.CustomState.Value = $"{MigratedWholesaleCalculationIdCustomStatePrefix}{wholesaleCalculation.Id}";

            var calculationInput = new CalculationInputV1(
                CalculationType: ToCalculationType(wholesaleCalculation.CalculationType),
                GridAreaCodes: wholesaleCalculation.GridAreaCodes.Select(x => x.Code).ToList(),
                PeriodStartDate: ToDateTimeOffset(wholesaleCalculation.PeriodStart),
                PeriodEndDate: ToDateTimeOffset(wholesaleCalculation.PeriodEnd),
                IsInternalCalculation: wholesaleCalculation.IsInternalCalculation);
            orchestrationInstance.ParameterValue.SetFromInstance(calculationInput);

            // Running
            orchestrationInstance.Lifecycle.TransitionToRunning(new MagicClock(wholesaleCalculation.ExecutionTimeStart));

            // Step: Calculation
            orchestrationInstance.TransitionStepToRunning(1, new MagicClock(wholesaleCalculation.ExecutionTimeStart));
            orchestrationInstance.TransitionStepToTerminated(1, OrchestrationStepTerminationState.Succeeded, new MagicClock(wholesaleCalculation.ExecutionTimeEnd));

            // Step: Enqueue messages
            var stepsSkippedBySequence = orchestrationInstance.Steps
                .Where(step => step.IsSkipped())
                .Select(step => step.Sequence)
                .ToList();
            if (!stepsSkippedBySequence.Contains(2))
            {
                orchestrationInstance.TransitionStepToRunning(2, new MagicClock(wholesaleCalculation.ActorMessagesEnqueuingTimeStart));
                orchestrationInstance.TransitionStepToTerminated(2, OrchestrationStepTerminationState.Succeeded, new MagicClock(wholesaleCalculation.ActorMessagesEnqueuedTimeEnd));
            }

            // Terminated
            orchestrationInstance.Lifecycle.TransitionToSucceeded(new MagicClock(wholesaleCalculation.CompletedTime));

            await _processManagerContext.OrchestrationInstances
                .AddAsync(orchestrationInstance)
                .ConfigureAwait(false);
            await _processManagerContext
                .CommitAsync()
                .ConfigureAwait(false);
        }

        return $"Migrated {input.CalculationToMigrateId}";
    }

    private static CalculationType ToCalculationType(Wholesale.Model.CalculationType calculationType)
    {
        switch (calculationType)
        {
            case Wholesale.Model.CalculationType.BalanceFixing:
                return CalculationType.BalanceFixing;
            case Wholesale.Model.CalculationType.Aggregation:
                return CalculationType.Aggregation;
            case Wholesale.Model.CalculationType.WholesaleFixing:
                return CalculationType.WholesaleFixing;
            case Wholesale.Model.CalculationType.FirstCorrectionSettlement:
                return CalculationType.FirstCorrectionSettlement;
            case Wholesale.Model.CalculationType.SecondCorrectionSettlement:
                return CalculationType.SecondCorrectionSettlement;
            case Wholesale.Model.CalculationType.ThirdCorrectionSettlement:
                return CalculationType.ThirdCorrectionSettlement;
            default:
                throw new ArgumentOutOfRangeException(nameof(calculationType), calculationType, "Calculation type is invalid.");
        }
    }

    private static DateTimeOffset ToDateTimeOffset(Instant instant)
    {
        return instant.ToDateTimeOffset();
    }

    private async Task<OrchestrationInstance> CreateQueuedOrchestrationInstanceAsync(Wholesale.Model.Calculation wholesaleCalculation)
    {
        var brs_023_023_description = await _processManagerContext.OrchestrationDescriptions
            .AsNoTracking()
            .SingleAsync(x => x.UniqueName == OrchestrationDescriptionUniqueName.FromDto(Brs_023_027.V1))
            .ConfigureAwait(false);

        IReadOnlyCollection<int> skipStepsBySequence = wholesaleCalculation.IsInternalCalculation
            ? [EnqueueMessagesStep.EnqueueActorMessagesStepSequence]
            : [];

        return _orchestrationInstanceFactory.CreateQueuedOrchestrationInstance(
            brs_023_023_description,
            wholesaleCalculation.CreatedByUserId,
            wholesaleCalculation.CreatedTime,
            wholesaleCalculation.ScheduledAt,
            skipStepsBySequence);
    }

    /// <summary>
    /// We do not allow nulls, and they should not appear within the given dataset for migration
    /// </summary>
    /// <param name="timeOrNull"></param>
    private class MagicClock(Instant? timeOrNull) : IClock
    {
        private readonly Instant _time = (Instant)timeOrNull!;

        public Instant GetCurrentInstant()
        {
            return _time;
        }
    }

    public record ActivityInput(
        Guid CalculationToMigrateId);
}
