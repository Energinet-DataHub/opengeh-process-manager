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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class MeteringPointSubTypeValidationRule
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    public static IList<ValidationError> WrongMeteringPointSubTypeError => [new(
        Message: "Målepunktet skal være enten fysisk eller virtuelt/metering point must be either physical or virtual",
        ErrorCode: "D37")];

    private static IList<ValidationError> NoError => [];

    private static IReadOnlyCollection<MeteringPointSubType> AllowedMeteringPointSubTypes => new[]
    {
        MeteringPointSubType.Physical,
        MeteringPointSubType.Virtual,
    };

    public Task<IList<ValidationError>> ValidateAsync(ForwardMeteredDataBusinessValidatedDto subject)
    {
        if (subject.MeteringPointMasterData.Count == 0)
        {
            return Task.FromResult(NoError);
        }

        // Check if the metering point subtype is the same for all historic master data
        var uniqueMeteringPointSubTypes = subject.MeteringPointMasterData
            .Select(mpmd => mpmd.MeteringPointSubType)
            .Distinct()
            .ToList();
        if (uniqueMeteringPointSubTypes.Count() != 1)
        {
            return Task.FromResult(WrongMeteringPointSubTypeError);
        }

        // Check if the metering point subtype is allowed
        var meteringPointSubType = uniqueMeteringPointSubTypes.First();
        if (!AllowedMeteringPointSubTypes.Contains(meteringPointSubType))
        {
            return Task.FromResult(WrongMeteringPointSubTypeError);
        }

        return Task.FromResult(NoError);
    }
}
