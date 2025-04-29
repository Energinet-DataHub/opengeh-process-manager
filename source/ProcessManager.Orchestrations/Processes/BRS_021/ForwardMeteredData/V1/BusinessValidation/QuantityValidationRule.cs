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

    public static readonly ValidationError MaxNumberOfIntegers = new(
        Message: $"Kvantum må højst være {MaximinNumbersOfIntegers} tal, fejl ved position: {{PropertyName}}"
                 + $"/ A maximum of {MaximinNumbersOfIntegers} digits, error at position: {{PropertyName}}",
        ErrorCode: "E89");

    public static readonly ValidationError MaxNumberOfDecimals = new(
        Message: $"Kvantum må højst have {MaximumNumbersOfDecimals} decimaler, fejl ved position: {{PropertyName}}"
                 + $"/ A maximum of {MaximumNumbersOfDecimals} decimals, error at position: {{PropertyName}}",
        ErrorCode: "E89");

    private static readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    public Task<IList<ValidationError>> ValidateAsync(ForwardMeteredDataBusinessValidatedDto subject)
    {
        var errors = new List<ValidationError>();
        var measureData = subject.Input.MeteredDataList;

        foreach (var data in measureData)
        {
            if (data.EnergyQuantity == null)
            {
                continue;
            }

            if (!decimal.TryParse(data.EnergyQuantity, _culture, out var quantity))
            {
                errors.Add(QuantityMustBePositive.WithPropertyName(data.Position!));
                continue;
            }

            if (GetNumberOfIntegers(quantity) > MaximinNumbersOfIntegers)
                errors.Add(MaxNumberOfIntegers.WithPropertyName(data.Position!));

            if (GetNumberOfDecimals(quantity) > MaximumNumbersOfDecimals)
                errors.Add(MaxNumberOfDecimals.WithPropertyName(data.Position!));

            if (quantity < 0)
                errors.Add(QuantityMustBePositive.WithPropertyName(data.Position!));
        }

        return Task.FromResult<IList<ValidationError>>(errors);
    }

    private static int GetNumberOfIntegers(decimal value)
    {
        return Math.Truncate(value).ToString(_culture).Length;
    }

    private static int GetNumberOfDecimals(decimal value)
    {
        return value.Scale;
    }
}
