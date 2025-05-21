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

public class ResolutionValidationRule
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    public static IList<ValidationError> WrongResolutionError => [new(
        Message: "Forkert opløsning/Wrong resolution",
        ErrorCode: "D23")];

    private static IList<ValidationError> NoError => [];

    private static IReadOnlyCollection<Resolution> AllowedResolutions => new[]
    {
        Resolution.QuarterHourly,
        Resolution.Hourly,
        Resolution.Monthly,
    };

    public Task<IList<ValidationError>> ValidateAsync(
        ForwardMeteredDataBusinessValidatedDto subject)
    {
        if (subject.MeteringPointMasterData.Count == 0)
        {
            return Task.FromResult(NoError);
        }

        if (subject.Input.Resolution == null)
        {
            return Task.FromResult(WrongResolutionError);
        }

        var incomingResolution = Resolution.FromNameOrDefault(subject.Input.Resolution);
        if (!AllowedResolutions.Contains(incomingResolution))
        {
            return Task.FromResult(WrongResolutionError);
        }

        // Check if the resolution is same for all historic master data
        if (subject.MeteringPointMasterData
            .Select(mpmd => mpmd.Resolution)
            .Any(resolution => resolution != incomingResolution))
        {
            return Task.FromResult(WrongResolutionError);
        }

        // Monthly resolution can only be used for VEProduction metering points
        if (incomingResolution == Resolution.Monthly && !IsVeProductionMeteringPointType(subject))
        {
            return Task.FromResult(WrongResolutionError);
        }

        return Task.FromResult(NoError);
    }

    private static bool IsVeProductionMeteringPointType(ForwardMeteredDataBusinessValidatedDto subject)
    {
        var isVeProductionMeteringPointFromInput = MeteringPointType
            .FromNameOrDefault(subject.Input.MeteringPointType) == MeteringPointType.VeProduction;

        var isVeProductionMeteringPointFromMasterData = subject.MeteringPointMasterData
            .All(mpmd => mpmd.MeteringPointType == MeteringPointType.VeProduction);

        return isVeProductionMeteringPointFromInput && isVeProductionMeteringPointFromMasterData;
    }
}
