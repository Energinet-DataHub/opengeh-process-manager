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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.BusinessValidation;

public class QuantityQualityValidationRule
    : IBusinessValidationRule<ForwardMeasurementsBusinessValidatedDto>
{
    public static IList<ValidationError> InvalidQuality => [new(
        Message: "Invalid kvalitet / Invalid quality",
        ErrorCode: "D12")];

    private static IList<ValidationError> NoError => [];

    private static IReadOnlyCollection<Quality> AllowedQualities =>
    [
        Quality.NotAvailable,
        Quality.Estimated,
        Quality.AsProvided,
    ];

    public Task<IList<ValidationError>> ValidateAsync(ForwardMeasurementsBusinessValidatedDto subject)
    {
        foreach (var measurement in subject.Input.Measurements)
        {
            Quality? quality;
            quality = measurement.QuantityQuality is null
                ? Quality.AsProvided // No provided quality means "AsProvided"
                : Quality.FromNameOrDefault(measurement.QuantityQuality);

            if (quality == null || !AllowedQualities.Contains(quality))
                return Task.FromResult(InvalidQuality);
        }

        return Task.FromResult(NoError);
    }
}
