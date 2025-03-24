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

using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using NodaTime;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1;

internal class StartCalculationHandlerV1(
    DateTimeZone dateTimeZone,
    IStartOrchestrationInstanceCommands manager) :
        IStartOrchestrationInstanceCommandHandler<StartCalculationCommandV1, CalculationInputV1>
{
    private readonly DateTimeZone _dateTimeZone = dateTimeZone;

    public async Task<Guid> HandleAsync(StartCalculationCommandV1 command)
    {
        GuardInputParameter(command.InputParameter);

        var orchestrationInstanceId = await manager
            .StartNewOrchestrationInstanceAsync(
                identity: command.OperatingIdentity.MapToDomain(),
                uniqueName: command.OrchestrationDescriptionUniqueName.MapToDomain(),
                inputParameter: command.InputParameter,
                skipStepsBySequence: [])
            .ConfigureAwait(false);

        return orchestrationInstanceId.Value;
    }

    /// <summary>
    /// Validate if input parameters are valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">If parameter input is not valid and exception is thrown that
    /// contains validation errors in its message property.</exception>
    private void GuardInputParameter(CalculationInputV1 inputParameter)
    {
        // Check that  the period start is midnight local time
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
            validationErrors.Add($"The period start '{inputParameter.PeriodStartDate}' must be midnight.");
        if (periodEndInTimeZone.TimeOfDay != LocalTime.Midnight)
            validationErrors.Add($"The period end '{inputParameter.PeriodEndDate}' must be midnight.");

        var daysBetween = Period.Between(periodStartInTimeZone.Date, periodEndInTimeZone.Date, PeriodUnits.Days).Days;
        if (daysBetween > 31)
            validationErrors.Add($"The period must not exceed 31 days.");

        if (validationErrors.Any())
            throw new InvalidOperationException(string.Join(" ", validationErrors));
    }
}
