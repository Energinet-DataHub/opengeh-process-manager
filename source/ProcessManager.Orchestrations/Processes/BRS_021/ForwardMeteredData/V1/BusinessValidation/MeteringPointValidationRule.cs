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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class MeteringPointValidationRule
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    public static IList<ValidationError> MeteringPointDoesntExistsError => [new(
        Message: "Målepunktet findes ikke / The metering point does not exist",
        ErrorCode: "E10")];

    private static IList<ValidationError> NoError => [];

    public Task<IList<ValidationError>> ValidateAsync(
        ForwardMeteredDataBusinessValidatedDto subject)
    {
        // This validation rule always succeeds, since Electricity Market doesn't contain data on dev environments.
        // TODO: Reimplement this validation rule when the Electricity Market has data on all environments.
        return Task.FromResult(NoError);
        // if (subject.MeteringPointMasterData.Count == 0)
        //     return Task.FromResult(MeteringPointDoesntExistsError);
        //
        // return Task.FromResult(NoError);
    }
}
