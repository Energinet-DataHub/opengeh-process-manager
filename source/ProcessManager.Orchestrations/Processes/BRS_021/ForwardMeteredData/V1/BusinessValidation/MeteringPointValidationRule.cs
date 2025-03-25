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
using Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

/// <summary>
/// Business validation rule for metering point validation
/// if the metering point does not exist, a business validation error is returned
/// </summary>
public class MeteringPointValidationRule(IFeatureFlagManager featureFlagManager)
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    private readonly IFeatureFlagManager _featureFlagManager = featureFlagManager;

    public static IList<ValidationError> MeteringPointDoesntExistsError => [new(
        Message: "Målepunktet findes ikke / The metering point does not exist",
        ErrorCode: "E10")];

    public static IList<ValidationError> MeteringPointConnectionStateError => [new(
        Message: "Målepunktet skal have status tilsluttet eller afbrudt/meteringpoint must have status connected or disconnected",
        ErrorCode: "D16")];

    private static IList<ValidationError> NoError => [];

    public async Task<IList<ValidationError>> ValidateAsync(
        ForwardMeteredDataBusinessValidatedDto subject)
    {
        if (await _featureFlagManager.IsEnabledAsync(FeatureFlag.EnableBrs021ForwardMeteredDataBusinessValidationForMeteringPoint).ConfigureAwait(false))
        {
            if (subject.MeteringPointMasterData.Count == 0)
                return MeteringPointDoesntExistsError;
        }

        if (subject.MeteringPointMasterData
            .Any(x => x.ConnectionState != ConnectionState.Connected && x.ConnectionState != ConnectionState.Disconnected))
        {
            return MeteringPointConnectionStateError;
        }

        return NoError;
    }
}
