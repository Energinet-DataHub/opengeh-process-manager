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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Extensions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1.BusinessValidation;

public class PeriodValidationRule(PeriodValidator periodValidator)
        : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    private const int MaxAllowedPeriodAgeInYears = 3;

    private const int MinimalPeriodSizeInHours = 4;

    public static readonly ValidationError InvalidEndDate = new(
        Message: "Slut dato mangler eller er ugyldig / End date is missing or invalid",
        ErrorCode: "E50");

    public static readonly ValidationError InvalidStartDate = new(
        Message: "Start dato mangler eller er ugyldig / Start date is missing or invalid",
        ErrorCode: "E50");

    public static readonly ValidationError StartDateIsTooOld = new(
        Message: $"Måledata er ældre end de tilladte {MaxAllowedPeriodAgeInYears} år / Measurements are older than the allowed {MaxAllowedPeriodAgeInYears} years",
        ErrorCode: "E17");

    public static readonly ValidationError StartMustBeBeforeEnd = new(
        Message: "Start tidspunktet skal være før slut tidspunktet / Start of period must be before end of period",
        ErrorCode: "E50");

    public static readonly ValidationError PeriodMustBeGreaterThan4Hours = new(
        Message: $"Tidsintervallet skal være større end {MinimalPeriodSizeInHours} timer når man anvender opløsningen: {{PropertyName}} "
                 + $"/ The time interval must be greater than {MinimalPeriodSizeInHours} hours when using the resolution: {{PropertyName}}",
        ErrorCode: "E50");

    public static readonly ValidationError MinuteIsNotAWholeQuarter = new(
        Message: "Forkert format for {PropertyName} tidspunkt. {PropertyName} tidspunkt skal være xx:00, xx:15, xx:30 eller xx:45 for PT15M opløsning "
                 + "/ Incorrect format for {PropertyName} time. {PropertyName} time must be xx:00, xx:15, xx:30 or xx:45 for PT15M resolution",
        ErrorCode: "D66");

    public static readonly ValidationError HourIsNotAWholeHour = new(
        Message: "Forkert format for {PropertyName} tidspunkt. {PropertyName} tidspunkt skal være xx:00 for PT1H opløsning "
                 + "/ Incorrect format for {PropertyName} time. {PropertyName} time must be xx:00 for PT1H resolution",
        ErrorCode: "D66");

    public static readonly ValidationError IsNotFirstOfMonthMidnightSummertime = new(
        Message: "Forkert dato format for {PropertyName}, skal være YYYY-MM-{Sidste dag i måneden}T22:00:00Z "
                 + "/ Wrong date format for {PropertyName}, must be YYYY-MM-{Last day of month}T22:00:00Z",
        ErrorCode: "D66");

    public static readonly ValidationError IsNotFirstOfMonthMidnightWintertime = new(
        Message: "Forkert dato format for {PropertyName}, skal være YYYY-MM-{Sidste dag i måneden}T23:00:00Z "
                 + "/ Wrong date format for {PropertyName}, must be YYYY-MM-{Last day of month}T23:00:00Z",
        ErrorCode: "D66");

    private readonly PeriodValidator _periodValidator = periodValidator;

    public Task<IList<ValidationError>> ValidateAsync(ForwardMeteredDataBusinessValidatedDto subject)
    {
        List<ValidationError> errors = [];

        var start = TryParseInstant(subject.Input.StartDateTime);
        var end = TryParseInstant(subject.Input.EndDateTime);

        if (end is null)
            errors.Add(InvalidEndDate);

        if (start is null)
            errors.Add(InvalidStartDate);

        if (errors.Any())
            return Task.FromResult((IList<ValidationError>)errors);

        if (_periodValidator.IsDateOlderThanAllowed(start!.Value, MaxAllowedPeriodAgeInYears, 0))
            errors.Add(StartDateIsTooOld);

        if (!start.Value.IsBefore(end!.Value))
            errors.Add(StartMustBeBeforeEnd);

        errors.AddRange(PeriodValidationForResolution(subject.Input.Resolution, start.Value, end.Value));

        return Task.FromResult((IList<ValidationError>)errors);
    }

    private IList<ValidationError> PeriodValidationForResolution(string? resolution, Instant start, Instant end)
    {
        return resolution switch
        {
            var res when res == Resolution.QuarterHourly.Name => PerformPeriodValidationForQuarterHourlyResolution(start, end),
            var res when res == Resolution.Hourly.Name => PerformPeriodValidationForHourlyResolution(start, end),
            var res when res == Resolution.Monthly.Name => PerformPeriodValidationForMonthlyResolution(start, end),
            _ => [],
        };
    }

    private IList<ValidationError> PerformPeriodValidationForQuarterHourlyResolution(Instant start, Instant end)
    {
        var errors = new List<ValidationError>();

        if (start.IsNotMultipleOf(Duration.FromMinutes(15)))
            errors.Add(MinuteIsNotAWholeQuarter.WithPropertyName(nameof(start)));

        if (end.IsNotMultipleOf(Duration.FromMinutes(15)))
            errors.Add(MinuteIsNotAWholeQuarter.WithPropertyName(nameof(end)));

        if (PeriodIsShorterThanMinimalPeriod(start, end))
            errors.Add(PeriodMustBeGreaterThan4Hours.WithPropertyName("PT15M"));

        return errors;
    }

    private IList<ValidationError> PerformPeriodValidationForHourlyResolution(Instant start, Instant end)
    {
        var errors = new List<ValidationError>();

        if (start.IsNotMultipleOf(Duration.FromHours(1)))
            errors.Add(HourIsNotAWholeHour.WithPropertyName(nameof(start)));

        if (end.IsNotMultipleOf(Duration.FromHours(1)))
            errors.Add(HourIsNotAWholeHour.WithPropertyName(nameof(end)));

        if (PeriodIsShorterThanMinimalPeriod(start, end))
            errors.Add(PeriodMustBeGreaterThan4Hours.WithPropertyName("PT1H"));

        return errors;
    }

    private IList<ValidationError> PerformPeriodValidationForMonthlyResolution(Instant start, Instant end)
    {
        return [
            ..MustBeMidnightFirstDayOfMonth(start, nameof(start)),
            ..MustBeMidnightFirstDayOfMonth(end, nameof(end))
        ];
    }

    private IList<ValidationError> MustBeMidnightFirstDayOfMonth(Instant instant, string propertyName)
    {
        if (_periodValidator.IsMidnight(instant, out var zonedDateTime) && zonedDateTime.Day == 1)
            return [];

        var error = zonedDateTime.IsDaylightSavingTime()
                ? IsNotFirstOfMonthMidnightSummertime.WithPropertyName(propertyName)
                : IsNotFirstOfMonthMidnightWintertime.WithPropertyName(propertyName);

        return [error];
    }

    private bool PeriodIsShorterThanMinimalPeriod(Instant start, Instant end)
    {
        var periodInTicks = double.Abs((end - start).TotalTicks);

        return periodInTicks < Duration.FromHours(MinimalPeriodSizeInHours).TotalTicks;
    }

    private Instant? TryParseInstant(string? dateTime)
    {
        if (dateTime is null)
            return null;

        var parseResult = InstantPatternWithOptionalSeconds.Parse(dateTime);

        if (!parseResult.Success)
            return null;

        return parseResult.Value;
    }
}
