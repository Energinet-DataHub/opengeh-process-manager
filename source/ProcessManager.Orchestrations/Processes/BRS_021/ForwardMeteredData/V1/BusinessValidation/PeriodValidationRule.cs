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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

/// <summary>
/// TODO: Implement correct validation rule
/// "Stub" period validation rule, used to verify that validation works for <see cref="ForwardMeteredDataBusinessValidatedDto"/>.
/// </summary>
public class PeriodValidationRule(PeriodValidator periodValidator)
        : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    /// <summary>
    /// TODO: Add correct error messages.
    /// "Stub" error message used to very that validation works.
    /// </summary>
    public static readonly ValidationError InvalidEndDate = new(
        Message: "Invalid slut dato / Invalid end date",
        ErrorCode: "E42");

    /// <summary>
    /// TODO: Add correct error messages.
    /// "Stub" error message used to very that validation works.
    /// </summary>
    public static readonly ValidationError InvalidStartDate = new(
        Message: "Invalid start dato / Invalid start date",
        ErrorCode: "E42");

    public static readonly ValidationError StartDateIsTooOld = new(
        Message:
        "Måledata tilhører ikke den tilladte tidsperiode / Measurements do not belong to the expected time period",
        ErrorCode: "E17");

    private readonly PeriodValidator _periodValidator = periodValidator;

    public Task<IList<ValidationError>> ValidateAsync(ForwardMeteredDataBusinessValidatedDto subject)
    {
        IList<ValidationError> errors = [];

        var start = TryParseInstant(subject.Input.StartDateTime);
        var end = TryParseInstant(subject.Input.EndDateTime);

        if (start is null)
            errors.Add(InvalidStartDate);

        if (end is null)
            errors.Add(InvalidEndDate);

        if (start is null)
            return Task.FromResult(errors);

        // Data age check
        if (_periodValidator.IsDateOlderThanAllowed(start.Value, 3, 0))
        {
            errors.Add(StartDateIsTooOld);
        }

        return Task.FromResult(errors);
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
