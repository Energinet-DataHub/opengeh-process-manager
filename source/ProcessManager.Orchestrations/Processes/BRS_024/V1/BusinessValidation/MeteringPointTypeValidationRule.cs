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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.BusinessValidation;

public class MeteringPointTypeValidationRule
    : IBusinessValidationRule<RequestYearlyMeasurementsBusinessValidatedDto>
{
    public static IList<ValidationError> WrongMeteringPointTypeError => [new(
        Message: "I forbindelse med anmodning om årssum kan der kun anmodes om data for forbrug og produktion/When requesting yearly amount then it is only possible to request for production and consumption",
        ErrorCode: "D18")];

    private static IList<ValidationError> NoError => [];

    private static IReadOnlyCollection<MeteringPointType> AllowedMeteringPointTypes =>
    [
        MeteringPointType.Production,
        MeteringPointType.Consumption,
    ];

    public Task<IList<ValidationError>> ValidateAsync(RequestYearlyMeasurementsBusinessValidatedDto subject)
    {
        if (subject.MeteringPointMasterData is null)
        {
            return Task.FromResult(NoError);
        }

        var masterDataMeteringPointType = subject.MeteringPointMasterData.MeteringPointType;
        if (!AllowedMeteringPointTypes.Contains(masterDataMeteringPointType))
        {
            return Task.FromResult(WrongMeteringPointTypeError);
        }

        return Task.FromResult(NoError);
    }
}
