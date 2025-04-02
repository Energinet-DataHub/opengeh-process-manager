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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;

public class PeriodValidationRule(PeriodValidator periodValidator)
    : IBusinessValidationRule<RequestCalculatedEnergyTimeSeriesInputV1>
{
    private const int MaxAllowedPeriodSizeInMonths = 1;
    private const int AllowedTimeFrameYearsFromNow = 3;
    private const int AllowedTimeFrameMonthsFromNow = 6;

    private static readonly ValidationError _invalidDateFormat = new("Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT22:00:00Z eller YYYY-MM-DDT23:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT22:00:00Z or YYYY-MM-DDT23:00:00Z", "D66");
    private static readonly ValidationError _invalidWinterMidnightFormat = new("Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT23:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT23:00:00Z", "D66");
    private static readonly ValidationError _invalidSummerMidnightFormat = new("Forkert dato format for {PropertyName}, skal være YYYY-MM-DDT22:00:00Z / Wrong date format for {PropertyName}, must be YYYY-MM-DDT22:00:00Z", "D66");
    private static readonly ValidationError _startDateMustBeLessThen3Years = new($"Dato må max være {AllowedTimeFrameYearsFromNow} år og {AllowedTimeFrameMonthsFromNow} måneder tilbage i tid / Can maximum be {AllowedTimeFrameYearsFromNow} years and {AllowedTimeFrameMonthsFromNow} months back in time", "E17");
    private static readonly ValidationError _periodIsGreaterThenAllowedPeriodSize = new("Dato må kun være for 1 måned af gangen / Can maximum be for a 1 month period", "E17");
    private static readonly ValidationError _missingStartOrAndEndDate = new("Start og slut dato skal udfyldes / Start and end date must be present in request", "E50");

    private readonly PeriodValidator _periodValidator = periodValidator;

    public Task<IList<ValidationError>> ValidateAsync(RequestCalculatedEnergyTimeSeriesInputV1 subject)
    {
        IList<ValidationError> errors = new List<ValidationError>();

        if (MissingDates(subject.PeriodStart, subject.PeriodEnd, errors))
            return Task.FromResult(errors);

        var startInstant = ParseToInstant(subject.PeriodStart, "Start date", errors);
        var endInstant = ParseToInstant(subject.PeriodEnd, "End date", errors);

        if (startInstant == null || endInstant == null)
            return Task.FromResult(errors);

        MustBeMidnight(startInstant.Value, "Start date", errors);
        MustBeMidnight(endInstant.Value, "End date", errors);

        StartDateMustBeGreaterThenAllowedYears(startInstant.Value, errors);
        IntervalMustBeWithinAllowedPeriodSize(startInstant.Value, endInstant.Value, errors);

        return Task.FromResult(errors);
    }

    private bool MissingDates(string start, [NotNullWhen(false)] string? end, IList<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
        {
            errors.Add(_missingStartOrAndEndDate);
            return true;
        }

        return false;
    }

    private void IntervalMustBeWithinAllowedPeriodSize(Instant start, Instant end, IList<ValidationError> errors)
    {
        if (_periodValidator.IntervalMustBeLessThanAllowedPeriodSize(start, end, MaxAllowedPeriodSizeInMonths))
            errors.Add(_periodIsGreaterThenAllowedPeriodSize);
    }

    private void StartDateMustBeGreaterThenAllowedYears(Instant start, IList<ValidationError> errors)
    {
        if (_periodValidator.IsMonthOfDateOlderThanXYearsAndYMonths(start, AllowedTimeFrameYearsFromNow, AllowedTimeFrameMonthsFromNow))
        {
            errors.Add(_startDateMustBeLessThen3Years);
        }
    }

    private Instant? ParseToInstant(string dateTimeString, string propertyName, IList<ValidationError> errors)
    {
        var parseResult = InstantPattern.General.Parse(dateTimeString);
        if (parseResult.Success)
            return parseResult.Value;

        errors.Add(_invalidDateFormat.WithPropertyName(propertyName));
        return null;
    }

    private void MustBeMidnight(Instant instant, string propertyName, IList<ValidationError> errors)
    {
        if (_periodValidator.IsMidnight(instant, out var zonedDateTime))
            return;

        errors.Add(zonedDateTime.IsDaylightSavingTime()
            ? _invalidSummerMidnightFormat.WithPropertyName(propertyName)
            : _invalidWinterMidnightFormat.WithPropertyName(propertyName));
    }
}
