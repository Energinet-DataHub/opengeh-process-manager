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
    public static IList<ValidationError> DuplicatedPositionError(IEnumerable<int> duplicates)
    {
        var duplicatesAsString = string.Join(", ", duplicates);

        return
        [
            new(
                Message:
                $"Position(erne) '{duplicatesAsString}' er duplikeret / The position(s) '{duplicatesAsString}' are duplicated",
                ErrorCode: "E87"),
        ];
    }

    public static IList<ValidationError> PositionsNotConsecutiveError(IEnumerable<int> missing)
    {
        var missingAsString = string.Join(", ", missing);

        return
        [
            new(
                Message:
                $"Position(erne) '{missingAsString}' mangler / The position(s) '{missingAsString}' are missing",
                ErrorCode: "E87"),
        ];
    }

    public static IList<ValidationError> IncorrectNumberOfPositionsError(int actual, double expected) =>
    [
        new(
            Message:
            $"Antal faktiske positioner ({actual}) svarer ikke til det forventede antal ({expected}) givet tidsopløsning og periodelængde / The actual position count ({actual}) does not match the expected count ({expected}) given resolution and period length",
            ErrorCode: "E87"),
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
        var errors = new List<ValidationError>();

        var period = Period.Between(
            startDate.ToDateTimeUtc().ToLocalDateTime(),
            endDate.ToDateTimeUtc().ToLocalDateTime(),
            PeriodUnits.Months | PeriodUnits.Days);

        // In case there is a residual,
        // the expected count will be a decimal number and as such will never be equal to the actual count
        var expectedPositionCount = subject.Input.Resolution switch
        {
            var pt15m when pt15m == Resolution.QuarterHourly.Name => (endDate - startDate).TotalMinutes / 15d,
            var pt1h when pt1h == Resolution.Hourly.Name => (endDate - startDate).TotalHours,
            var p1d when p1d == Resolution.Daily.Name => (endDate - startDate).TotalDays,
            var p1m when p1m == Resolution.Monthly.Name => period.Months + (period.Days / 100d),
            _ => 0d,
        };

        // As the expected count is a decimal number,
        // it might have some residuals by virtue of being a double,
        // and we would like to ignore those as actual errors.
        // At the same time,
        // we have to catch those residuals that represent a real error from an incorrect period length.
        if (Math.Abs(subject.Input.MeteredDataList.Count - expectedPositionCount) > 0.000001d)
        {
            errors.AddRange(
                IncorrectNumberOfPositionsError(subject.Input.MeteredDataList.Count, expectedPositionCount));
        }

        /*
         * This will blow up if the position is null or not a number.
         * But the value is required in the schema, and is defined as
         * "value": {
         *     "description": "Main Core value Space.",
         *     "type": "integer",
         *     "maximum": 999999,
         *     "minimum": 1
         * }
         * in JSON
         * <xs:restriction base="xs:integer">
         *     <xs:maxInclusive value="999999"/>
         *     <xs:minInclusive value="1"/>
         * </xs:restriction>
         * in XML, and finally
         * <xsd:restriction base="xsd:integer">
         *     <xsd:totalDigits value="10" />
         * </xsd:restriction>
         * in eBix.
         */
        var positions = subject.Input.MeteredDataList.Select(md => int.Parse(md.Position!)).Order().ToList();

        var duplicates = positions.GroupBy(p => p).Where(g => g.Count() > 1).ToList();
        if (duplicates.Count > 0)
        {
            errors.AddRange(DuplicatedPositionError(duplicates.Select(g => g.Key)));
        }

        if (positions.First() != 1 || positions.Last() != positions.Count)
        {
            errors.AddRange(PositionsNotConsecutiveError(Enumerable.Range(1, positions.Count).Except(positions)));
        }

        return Task.FromResult<IList<ValidationError>>(errors);
    }
}
