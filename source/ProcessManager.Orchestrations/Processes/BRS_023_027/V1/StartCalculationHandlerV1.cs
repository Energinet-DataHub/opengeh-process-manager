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

using Energinet.DataHub.ProcessManagement.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using NodaTime;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1;

internal class StartCalculationHandlerV1(
    DateTimeZone dateTimeZone,
    IStartOrchestrationInstanceCommands manager) :
        IStartOrchestrationInstanceCommandHandler<StartCalculationCommandV1, CalculationInputV1>,
        IScheduleOrchestrationInstanceCommandHandler<ScheduleCalculationCommandV1, CalculationInputV1>
{
    private readonly DateTimeZone _dateTimeZone = dateTimeZone;
    private readonly IStartOrchestrationInstanceCommands _manager = manager;

    public async Task<Guid> HandleAsync(StartCalculationCommandV1 command)
    {
        GuardInputParameter(command.InputParameter);

        // Here we show how its possible, based on input, to decide certain steps should be skipped by the orchestration.
        IReadOnlyCollection<int> skipStepsBySequence = command.InputParameter.IsInternalCalculation
            ? [Orchestration_Brs_023_027_V1.EnqueueMessagesStepSequence]
            : [];

        var orchestrationInstanceId = await _manager
            .StartNewOrchestrationInstanceAsync(
                identity: new UserIdentity(
                    new UserId(command.OperatingIdentity.UserId),
                    new ActorId(command.OperatingIdentity.ActorId)),
                uniqueName: new OrchestrationDescriptionUniqueName(
                    command.OrchestrationDescriptionUniqueName.Name,
                    command.OrchestrationDescriptionUniqueName.Version),
                inputParameter: command.InputParameter,
                skipStepsBySequence: skipStepsBySequence)
            .ConfigureAwait(false);

        return orchestrationInstanceId.Value;
    }

    public async Task<Guid> HandleAsync(ScheduleCalculationCommandV1 command)
    {
        GuardInputParameter(command.InputParameter);

        // Here we show how its possible, based on input, to decide certain steps should be skipped by the orchestration.
        IReadOnlyCollection<int> skipStepsBySequence = command.InputParameter.IsInternalCalculation
            ? [Orchestration_Brs_023_027_V1.EnqueueMessagesStepSequence]
            : [];

        var orchestrationInstanceId = await _manager
            .ScheduleNewOrchestrationInstanceAsync(
                identity: new UserIdentity(
                    new UserId(command.OperatingIdentity.UserId),
                    new ActorId(command.OperatingIdentity.ActorId)),
                uniqueName: new OrchestrationDescriptionUniqueName(
                    command.OrchestrationDescriptionUniqueName.Name,
                    command.OrchestrationDescriptionUniqueName.Version),
                inputParameter: command.InputParameter,
                runAt: command.RunAt.ToInstant(),
                skipStepsBySequence: skipStepsBySequence)
            .ConfigureAwait(false);

        return orchestrationInstanceId.Value;
    }

    private static bool IsEntireMonth(ZonedDateTime periodStart, ZonedDateTime periodEnd)
    {
        return periodStart.Day == 1 && periodEnd.LocalDateTime == periodStart.LocalDateTime.PlusMonths(1);
    }

    /// <summary>
    /// Validate if input parameters are valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">If parameter input is not valid and exception is thrown that
    /// contains validation errors in its message property.</exception>
    private void GuardInputParameter(CalculationInputV1 inputParameter)
    {
        var validationErrors = new List<string>();

        if (!inputParameter.GridAreaCodes.Any())
            validationErrors.Add("Must contain at least one grid area code.");

        if (inputParameter.PeriodStartDate >= inputParameter.PeriodEndDate)
            validationErrors.Add($"'{nameof(inputParameter.PeriodStartDate)}' is greater or equal to '{nameof(inputParameter.PeriodEndDate)}'.");

        var periodStart = inputParameter.PeriodStartDate.ToInstant();
        var periodStartInTimeZone = new ZonedDateTime(periodStart, _dateTimeZone);

        var periodEnd = inputParameter.PeriodEndDate.ToInstant();
        var periodEndInTimeZone = new ZonedDateTime(periodEnd, _dateTimeZone);

        // Validate that period start/end are set to midnight
        if (periodStartInTimeZone.TimeOfDay != LocalTime.Midnight)
            validationErrors.Add($"The period start '{periodStart}' must be midnight.");
        if (periodEndInTimeZone.TimeOfDay != LocalTime.Midnight)
            validationErrors.Add($"The period end '{periodEnd}' must be midnight.");

        if (inputParameter.CalculationType is CalculationTypes.WholesaleFixing
            or CalculationTypes.FirstCorrectionSettlement
            or CalculationTypes.SecondCorrectionSettlement
            or CalculationTypes.ThirdCorrectionSettlement)
        {
            if (!IsEntireMonth(periodStartInTimeZone, periodEndInTimeZone))
            {
                validationErrors.Add($"The period (start: '{periodStart}' end: '{periodEnd}') has to be an entire month when using calculation type '{inputParameter.CalculationType}'.");
            }
        }

        if (inputParameter.IsInternalCalculation && inputParameter.CalculationType is not CalculationTypes.Aggregation)
            validationErrors.Add($"Internal calculations is not allowed for '{inputParameter.CalculationType}'.");

        if (validationErrors.Any())
            throw new InvalidOperationException(string.Join(" ", validationErrors));
    }
}
