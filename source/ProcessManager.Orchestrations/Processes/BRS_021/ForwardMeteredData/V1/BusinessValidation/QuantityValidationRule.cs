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

using System.Globalization;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class QuantityValidationRule
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    private const int MaximinNumbersOfIntegers = 10;
    private const int MaximumNumbersOfDecimals = 3;

    public static readonly ValidationError QuantityMustBePositive = new(
        Message: "Kvantum skal være et positivt tal, fejl ved position: {PropertyName} "
                 + "/ Quantity must be a positive number, error at position: {PropertyName}",
        ErrorCode: "E86");

    public static readonly ValidationError WrongFormatForQuantity = new(
        Message: $"Kvantum må højst være {MaximinNumbersOfIntegers} tal og med max {MaximumNumbersOfDecimals} decimaler, fejl ved position: {{PropertyName}} "
                 + $"/ A maximum of {MaximinNumbersOfIntegers} digits and {MaximumNumbersOfDecimals} decimals are allowed for quality, error at position: {{PropertyName}}",
        ErrorCode: "E86");

    public Task<IList<ValidationError>> ValidateAsync(ForwardMeteredDataBusinessValidatedDto subject)
    {
        var errors = new List<ValidationError>();
        var measureData = subject.Input.MeteredDataList;

        foreach (var data in measureData)
        {
            if (data.EnergyQuantity == null)
                errors.Add(QuantityMustBePositive.WithPropertyName(data.Position!)); // TODO: Position is nullable?

            decimal quantity;
            if (!decimal.TryParse(data.EnergyQuantity, out quantity))
                errors.Add(QuantityMustBePositive.WithPropertyName(data.Position!)); // TODO: Position is nullable?

            if (GetIntegers(quantity).ToString(CultureInfo.InvariantCulture).Length > MaximinNumbersOfIntegers)
                errors.Add(WrongFormatForQuantity.WithPropertyName(data.Position!)); // TODO: Position is nullable?

            //if (quantity != Math.Round(quantity, 3))
            if (GetDecimals(quantity).ToString(CultureInfo.InvariantCulture).Length > MaximumNumbersOfDecimals)
                errors.Add(WrongFormatForQuantity.WithPropertyName(data.Position!)); // TODO: Position is nullable?

            if (quantity < 0)
                errors.Add(QuantityMustBePositive.WithPropertyName(data.Position!)); // TODO: Position is nullable?
        }

        return Task.FromResult<IList<ValidationError>>(errors);
    }

    private static decimal GetIntegers(decimal value)
    {
        return Math.Floor(value);
    }

    private static decimal GetDecimals(decimal value)
    {
        return value - Math.Floor(value);
    }
}
