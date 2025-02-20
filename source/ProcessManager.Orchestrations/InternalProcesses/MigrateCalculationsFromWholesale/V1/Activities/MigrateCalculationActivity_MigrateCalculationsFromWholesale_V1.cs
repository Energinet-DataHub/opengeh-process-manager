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

internal class MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1(
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
        try
        {
            var wasMigrated = await WasCalculationIdMigratedAsync(input.CalculationToMigrateId).ConfigureAwait(false);
            if (wasMigrated)
                return $"Migration skipped for '{input.CalculationToMigrateId}'.";

            var wholesaleCalculation = await _wholesaleContext.Calculations
                    .FirstAsync(x => x.Id == input.CalculationToMigrateId)
                    .ConfigureAwait(false);

            var brs_023_027_V1_description = await _processManagerContext.OrchestrationDescriptions
                .AsNoTracking()
                .SingleAsync(x => x.UniqueName == OrchestrationDescriptionUniqueName.FromDto(Brs_023_027.V1))
                .ConfigureAwait(false);

            var orchestrationInstance = MigrateCalculationToOrchestrationInstance(wholesaleCalculation, brs_023_027_V1_description);

            await _processManagerContext.OrchestrationInstances
                .AddAsync(orchestrationInstance)
                .ConfigureAwait(false);
            await _processManagerContext
                .CommitAsync()
                .ConfigureAwait(false);

            return $"Migration succeeded for '{input.CalculationToMigrateId}'.";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Migration failed for '{input.CalculationToMigrateId}'.", ex);
        }
    }

    private static CalculationType ToCalculationType(Wholesale.Model.CalculationType calculationType)
    {
        return calculationType switch
        {
            Wholesale.Model.CalculationType.BalanceFixing => CalculationType.BalanceFixing,
            Wholesale.Model.CalculationType.Aggregation => CalculationType.Aggregation,
            Wholesale.Model.CalculationType.WholesaleFixing => CalculationType.WholesaleFixing,
            Wholesale.Model.CalculationType.FirstCorrectionSettlement => CalculationType.FirstCorrectionSettlement,
            Wholesale.Model.CalculationType.SecondCorrectionSettlement => CalculationType.SecondCorrectionSettlement,
            Wholesale.Model.CalculationType.ThirdCorrectionSettlement => CalculationType.ThirdCorrectionSettlement,
            _ => throw new ArgumentOutOfRangeException(nameof(calculationType), calculationType, "Calculation type is invalid."),
        };
    }

    private static DateTimeOffset ToDateTimeOffset(Instant instant)
    {
        return instant.ToDateTimeOffset();
    }

    private Task<bool> WasCalculationIdMigratedAsync(Guid wholesaleCalculationId)
    {
        return _processManagerContext.OrchestrationInstances
            .AsNoTracking()
            .Where(x => x.CustomState == GetMigratedWholesaleCalculationIdCustomState(wholesaleCalculationId))
            .AnyAsync();
    }

    private OrchestrationInstance MigrateCalculationToOrchestrationInstance(
        Wholesale.Model.Calculation wholesaleCalculation,
        OrchestrationDescription brs_023_027_V1_description)
    {
        // Orchestration Instance => Created + Queued
        IReadOnlyCollection<int> skipStepsBySequence = wholesaleCalculation.IsInternalCalculation
            ? [EnqueueMessagesStep.EnqueueActorMessagesStepSequence]
            : [];

        var orchestrationInstance = _orchestrationInstanceFactory.CreateQueuedOrchestrationInstance(
            brs_023_027_V1_description,
            wholesaleCalculation.CreatedByUserId,
            wholesaleCalculation.CreatedTime,
            wholesaleCalculation.ScheduledAt,
            skipStepsBySequence);

        orchestrationInstance.CustomState.Value = $"{MigratedWholesaleCalculationIdCustomStatePrefix}{wholesaleCalculation.Id}";

        var calculationInput = new CalculationInputV1(
            CalculationType: ToCalculationType(wholesaleCalculation.CalculationType),
            GridAreaCodes: wholesaleCalculation.GridAreaCodes.Select(x => x.Code).ToList(),
            PeriodStartDate: ToDateTimeOffset(wholesaleCalculation.PeriodStart),
            PeriodEndDate: ToDateTimeOffset(wholesaleCalculation.PeriodEnd),
            IsInternalCalculation: wholesaleCalculation.IsInternalCalculation);
        orchestrationInstance.ParameterValue.SetFromInstance(calculationInput);

        // Orchestration Instance => Running
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

        // Orchestration Instance => Terminated
        orchestrationInstance.Lifecycle.TransitionToSucceeded(new MagicClock(wholesaleCalculation.CompletedTime));

        return orchestrationInstance;
    }

    /// <summary>
    /// An implementation of IClock that allows us to manipulate time and set it to a decired value.
    /// This is used in state transitions to be able to make the transition happen at a certain time.
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
