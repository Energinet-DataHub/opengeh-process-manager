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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;

/// <summary>
/// Validation rule for the resolution property when there is requested for wholesale services.
/// </summary>
public class ResolutionValidationRule
    : IBusinessValidationRule<RequestCalculatedWholesaleServicesInputV1>
{
    private const string PropertyName = "aggregationSeries_Period.resolution";
    private static readonly ValidationError _notMonthlyResolution =
        new(
            $"{PropertyName} skal være 'P1M'/{PropertyName} must be 'P1M'",
            "D23");

    public Task<IList<ValidationError>> ValidateAsync(RequestCalculatedWholesaleServicesInputV1 subject)
    {
        var errors = new List<ValidationError>();
        if (subject.Resolution is not null
            && subject.Resolution != Resolution.Monthly.Name)
        {
            errors.Add(_notMonthlyResolution);
        }

        return Task.FromResult<IList<ValidationError>>(errors);
    }
}
