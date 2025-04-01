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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using NodaTime;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class PositionCountValidationRule : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    public static IList<ValidationError> IncorrectNumberOfPositionsError =>
    [
        new(
            Message:
            "Antal positioner svarer ikke til tidsopløsning og periodelængde / Position count does not match resolution and period length",
            ErrorCode: "E87"),
    ];

    public static IList<ValidationError> PeriodNotModError(double mod) =>
    [
        new(
            Message:
            $"Period not modulo resolution ({mod}) / Period not modulo resolution ({mod})",
            ErrorCode: "E99"),
    ];

    public Task<IList<ValidationError>> ValidateAsync(ForwardMeteredDataBusinessValidatedDto subject)
    {
        if (subject.Input.EndDateTime is null)
        {
            return Task.FromResult<IList<ValidationError>>([]);
        }

        var startDateResult = InstantPatternWithOptionalSeconds.Parse(subject.Input.StartDateTime);
        var endDateResult = InstantPatternWithOptionalSeconds.Parse(subject.Input.EndDateTime);

        if (!startDateResult.Success || !endDateResult.Success)
        {
            return Task.FromResult<IList<ValidationError>>([]);
        }

        var startDate = startDateResult.Value;
        var endDate = endDateResult.Value;
        var periodDuration = endDate - startDate;

        var expectedPositionCount = subject.Input.Resolution switch
        {
            var pt15m when pt15m == Resolution.QuarterHourly.Name => periodDuration.TotalMinutes % 15,
            var pt1h when pt1h == Resolution.Hourly.Name => periodDuration.TotalHours % 1,
            var p1d when p1d == Resolution.Daily.Name => periodDuration.TotalDays % 1,
            // This maybe worky, but of hacky... Or is?
            var p1m when p1m == Resolution.Monthly.Name => Period.Between(
                                                                   startDate.ToDateTimeUtc().ToLocalDateTime(),
                                                                   endDate.ToDateTimeUtc().ToLocalDateTime(),
                                                                   PeriodUnits.Months | PeriodUnits.Days)
                                                               .Days
                                                           != 0
                ? 0.25
                : Period.Between(
                        startDate.ToDateTimeUtc().ToLocalDateTime(),
                        endDate.ToDateTimeUtc().ToLocalDateTime(),
                        PeriodUnits.Months | PeriodUnits.Days)
                    .Months,
            _ => 0.5,
        };

        if (expectedPositionCount % 1 != 0)
        {
            return Task.FromResult(PeriodNotModError(expectedPositionCount));
        }

        var expectedPositionCountAsInt = (int)expectedPositionCount;
        if (subject.Input.MeteredDataList.Count != expectedPositionCountAsInt)
        {
            return Task.FromResult(IncorrectNumberOfPositionsError);
        }

        return Task.FromResult<IList<ValidationError>>([]);
    }
}
