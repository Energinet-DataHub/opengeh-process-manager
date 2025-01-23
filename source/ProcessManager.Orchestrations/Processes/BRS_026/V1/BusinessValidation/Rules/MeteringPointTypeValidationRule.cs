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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.BusinessValidation.Rules;

public class MeteringPointTypeValidationRule : IBusinessValidationRule<RequestCalculatedEnergyTimeSeriesInputV1>
{
    private static readonly IReadOnlyList<string> _validMeteringPointTypes =
    [
        MeteringPointType.Consumption.Name,
        MeteringPointType.Production.Name,
        MeteringPointType.Exchange.Name,
    ];

    private static readonly ValidationError _invalidMeteringPointType =
        new(
            "Metering point type skal være en af følgende: {PropertyName} eller undladt / Metering point type has one of the following: {PropertyName} or omitted",
            "D18");

    private static IList<ValidationError> NoError => [];

    private static IList<ValidationError> InvalidMeteringPointType => [_invalidMeteringPointType.WithPropertyName("E17, E18, E20")];

    public Task<IList<ValidationError>> ValidateAsync(RequestCalculatedEnergyTimeSeriesInputV1 subject)
    {
        if (subject.MeteringPointType is null)
            return Task.FromResult(NoError);

        if (_validMeteringPointTypes.Contains(subject.MeteringPointType, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(NoError);

        return Task.FromResult(InvalidMeteringPointType);
    }
}
