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
    public const string MissingStartOrEndDate =
        "Start og slutdato skal være udfyldt / Start and end date are required";

    public const string InvalidStartOrEnDate =
        "Start og slutdato skal være gyldige datoer / Start and end date must be valid dates";

    public const string StartDateAfterEndDate =
        "Startdato skal være før slutdato / Start date must be before end date";

    public const string StartOrEndDateAreNotMidnight =
        "Start og slutdato skal være midnat / Start and end date must be at midnight";

    public const string PeriodIsTooLong =
        "Det er kun tilladt at anmode om data for 1 år ad gangen / It is only allowed to request data for 1 year at a time";

    public const string PeriodIsTooOld =
        "Det er ikke tilladt at anmode om data for mere end 3 år siden / It is not allowed to request data older than 3 years";

    private readonly PeriodValidator _periodValidator = periodValidator;

    public Task<IList<ValidationError>> ValidateAsync(RequestMeasurementsBusinessValidatedDto subject)
    {
        List<ValidationError> errors = new();

        if (subject.Input.EndDateTime is null)
        {
            errors.Add(new(MissingStartOrEndDate, "E50"));
            return Task.FromResult<IList<ValidationError>>(errors);
        }

        var startDateParseResult = InstantPattern.General.Parse(subject.Input.StartDateTime);
        var endDateParseResult = InstantPattern.General.Parse(subject.Input.EndDateTime);

        if (!startDateParseResult.Success || !endDateParseResult.Success)
        {
            errors.Add(
                new(
                    InvalidStartOrEnDate,
                    "E50"));

            return Task.FromResult<IList<ValidationError>>(errors);
        }

        var startDate = startDateParseResult.Value;
        var endDate = endDateParseResult.Value;

        if (startDate > endDate)
        {
            errors.Add(new(StartDateAfterEndDate, "E50"));
        }

        if (!_periodValidator.IsMidnight(startDate, out _) || !_periodValidator.IsMidnight(endDate, out _))
        {
            errors.Add(new(StartOrEndDateAreNotMidnight, "E50"));
        }

        if (_periodValidator.IntervalMustBeLessThanAllowedPeriodSize(startDate, endDate, 12))
        {
            errors.Add(
                new(
                    PeriodIsTooLong,
                    "E50"));
        }

        if (_periodValidator.IsDateOlderThanAllowed(startDate, 3, 0))
        {
            errors.Add(
                new(
                    PeriodIsTooOld,
                    "E50"));
        }

        return Task.FromResult<IList<ValidationError>>(errors);
    }
}
