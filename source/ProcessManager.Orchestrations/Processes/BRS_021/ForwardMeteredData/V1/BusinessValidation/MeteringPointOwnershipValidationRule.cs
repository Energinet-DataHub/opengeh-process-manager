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
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

/// <summary>
/// Business validation rule for metering point validation
/// if the metering point does not exist, a business validation error is returned
/// </summary>
public class MeteringPointOwnershipValidationRule(IFeatureFlagManager featureFlagManager)
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    private readonly IFeatureFlagManager _featureFlagManager = featureFlagManager;

    public static IList<ValidationError> MeteringPointHasWrongOwnerError =>
    [
        new(
            Message: "Forkert ejer af målepunktet / wrong owner of metering point",
            ErrorCode: "D50"),
    ];

    private static IList<ValidationError> NoError => [];

    public async Task<IList<ValidationError>> ValidateAsync(
        ForwardMeteredDataBusinessValidatedDto subject)
    {
        if (await IsPerformanceTest(subject.Input).ConfigureAwait(false))
            return NoError;

        // All the historical metering point master data, have the current grid access provider provided.
        var meteringPointMasterData = subject.MeteringPointMasterData.FirstOrDefault();

        if (meteringPointMasterData != null && !meteringPointMasterData.CurrentGridAccessProvider.Value
                .Equals(subject.Input.GridAccessProviderNumber))
        {
            return MeteringPointHasWrongOwnerError;
        }

        return NoError;
    }

    private async Task<bool> IsPerformanceTest(ForwardMeteredDataInputV1 input)
    {
        var performanceTestEnabled = await _featureFlagManager
            .IsEnabledAsync(FeatureFlag.EnableBrs021ForwardMeteredDataPerformanceTest)
            .ConfigureAwait(false);
        var isInputTest = input.MeteringPointId?.IsTestUuid() ?? false;

        return performanceTestEnabled && isInputTest;
    }
}
