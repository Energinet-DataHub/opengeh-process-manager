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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;

public class ChargeCodeValidationRule : IBusinessValidationRule<RequestCalculatedWholesaleServicesInputV1>
{
    private static readonly ValidationError _chargeCodeLengthInvalidError = new(
        "Følgende chargeType mRID er for lang: {PropertyName}. Den må højst indeholde 10 karaktere/"
        + "The following chargeType mRID is to long: {PropertyName} It must at most be 10 characters",
        "D14");

    private static IList<ValidationError> NoError => [];

    public Task<IList<ValidationError>> ValidateAsync(RequestCalculatedWholesaleServicesInputV1 subject)
    {
        if (subject.ChargeTypes is null)
            return Task.FromResult(NoError);

        var chargeTypesWithTooLongType = subject.ChargeTypes
            .Where(chargeType =>
                chargeType.ChargeCode is not null
                && chargeType.ChargeCode.Length > 10)
            .ToList();

        if (chargeTypesWithTooLongType.Count == 0)
            return Task.FromResult(NoError);

        var errors = chargeTypesWithTooLongType
            .Select(chargeType =>
                _chargeCodeLengthInvalidError.WithPropertyName(chargeType.ChargeCode!))
            .ToList();

        return Task.FromResult<IList<ValidationError>>(errors);
    }
}
