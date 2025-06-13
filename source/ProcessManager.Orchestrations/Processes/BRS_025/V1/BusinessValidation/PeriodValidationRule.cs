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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Model;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.BusinessValidation;

public sealed class PeriodValidationRule(PeriodValidator periodValidator)
    : IBusinessValidationRule<RequestMeasurementsBusinessValidatedDto>
{
    private const int MaxAllowedPeriodSizeInMonths = 12;
    private const int MaxAgeOfDataInYears = 3;

    private readonly PeriodValidator _periodValidator = periodValidator;

    public static string MissingEndDate() =>
        "Slutdatoen skal være udfyldt / The end date is required";

    public static string InvalidStartDate(string date) =>
        $"Startdatoen '{date}' skal være en gyldig dato / The start date '{date}' must be a valid date";

    public static string InvalidEndDate(string date) =>
        $"Slutdatoen '{date}' skal være en gyldig dato / The end date '{date}' must be valid a date";

    public static string StartDateAfterEndDate(Instant start, Instant end) =>
        $"Startdatoen '{start}' skal være før slutdatoen '{end}' / The start date '{start}' must be before the end date '{end}'";

    public static string StartDateIsNotMidnight(Instant start) =>
        $"Startdatoen '{start}' skal være ved midnat / The start date '{start}' must be at midnight";

    public static string EndDateIsNotMidnight(Instant end) =>
        $"Slutdatoen '{end}' skal være ved midnat / The end date '{end}' must be at midnight";

    public static string PeriodIsTooLong(Period period) =>
        $"Det er kun tilladt at anmode om data for {MaxAllowedPeriodSizeInMonths} måneder af gangen, men der blev anmodet om {period.Months} måneder og {period.Days} dag(e) / It is only allowed to request data for {MaxAllowedPeriodSizeInMonths} months at a time, but {period.Months} months and {period.Days} day(s) was requested";

    public static string PeriodIsTooOld() =>
        "Det er ikke tilladt at anmode om data for mere end 3 år siden / It is not allowed to request data older than 3 years";

    public Task<IList<ValidationError>> ValidateAsync(RequestMeasurementsBusinessValidatedDto subject)
    {
        List<ValidationError> errors = new();

        if (subject.Input.EndDateTime is null)
        {
            errors.Add(new(MissingEndDate(), "E50"));
            return Task.FromResult<IList<ValidationError>>(errors);
        }

        var startDateParseResult = InstantPattern.General.Parse(subject.Input.StartDateTime);
        var endDateParseResult = InstantPattern.General.Parse(subject.Input.EndDateTime);

        if (!startDateParseResult.Success)
        {
            errors.Add(
                new(
                    InvalidStartDate(subject.Input.StartDateTime),
                    "E50"));
        }

        if (!endDateParseResult.Success)
        {
            errors.Add(
                new(
                    InvalidEndDate(subject.Input.EndDateTime),
                    "E50"));
        }

        // If either date is invalid, we can return early as we cannot proceed with validation
        if (errors.Any())
        {
            return Task.FromResult<IList<ValidationError>>(errors);
        }

        var startDate = startDateParseResult.Value;
        var endDate = endDateParseResult.Value;

        if (startDate > endDate)
        {
            errors.Add(new(StartDateAfterEndDate(startDate, endDate), "E50"));
        }

        if (!_periodValidator.IsMidnight(startDate, out _))
        {
            errors.Add(new(StartDateIsNotMidnight(startDate), "E50"));
        }

        if (!_periodValidator.IsMidnight(endDate, out _))
        {
            errors.Add(new(EndDateIsNotMidnight(endDate), "E50"));
        }

        if (_periodValidator.IntervalMustBeLessThanAllowedPeriodSize(startDate, endDate, MaxAllowedPeriodSizeInMonths))
        {
            var period = Period.Between(
                startDate.InUtc().LocalDateTime,
                endDate.InUtc().LocalDateTime,
                PeriodUnits.Months | PeriodUnits.Days);

            errors.Add(
                new(
                    PeriodIsTooLong(period),
                    "E50"));
        }

        if (_periodValidator.IsDateOlderThanAllowed(startDate, MaxAgeOfDataInYears, 0))
        {
            errors.Add(
                new(
                    PeriodIsTooOld(),
                    "E50"));
        }

        return Task.FromResult<IList<ValidationError>>(errors);
    }
}
