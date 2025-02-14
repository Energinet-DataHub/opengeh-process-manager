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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;

public class GridAreaValidationRule(
    IGridAreaOwnerClient gridAreaOwnerClient)
        : IBusinessValidationRule<RequestCalculatedEnergyTimeSeriesInputV1>
{
    private static readonly ValidationError _missingGridAreaCode = new("Netområde er obligatorisk for rollen MDR / Grid area is mandatory for the role MDR.", "D64");
    private static readonly ValidationError _invalidGridArea = new("Ugyldig netområde / Invalid gridarea", "E86");

    private readonly IGridAreaOwnerClient _gridAreaOwnerClient = gridAreaOwnerClient;

    private static IList<ValidationError> NoError => [];

    private static IList<ValidationError> MissingGridAreaCodeError => [_missingGridAreaCode];

    private static IList<ValidationError> InvalidGridAreaError => [_invalidGridArea];

    public async Task<IList<ValidationError>> ValidateAsync(RequestCalculatedEnergyTimeSeriesInputV1 subject)
    {
        if (subject.RequestedForActorRole != ActorRole.MeteredDataResponsible.Name)
            return NoError;

        if (subject.GridAreas.Count == 0)
            return MissingGridAreaCodeError;

        foreach (var gridArea in subject.GridAreas)
        {
            var isGridAreaOwner = await _gridAreaOwnerClient.IsCurrentOwnerAsync(
                    gridArea,
                    subject.RequestedForActorNumber,
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (!isGridAreaOwner)
                return InvalidGridAreaError;
        }

        return NoError;
    }
}
