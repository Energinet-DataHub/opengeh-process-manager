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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.BusinessValidation;

public sealed class ActorRoleValidationRule : IBusinessValidationRule<RequestYearlyMeasurementsBusinessValidatedDto>
{
    internal static IList<ValidationError> WrongActorRoleError => [new(
        "Det er kun elleverandøren der kan anmode om årssum/Only energy supplier is allowed to request yearly amounts.",
        "E16")];

    private static IList<ValidationError> NoError => [];

    public Task<IList<ValidationError>> ValidateAsync(RequestYearlyMeasurementsBusinessValidatedDto subject)
    {
        if (subject.MeteringPointMasterData is null)
        {
            return Task.FromResult(NoError);
        }

        if (subject.Input.ActorRole == ActorRole.EnergySupplier.Name)
            return Task.FromResult(NoError);

        return Task.FromResult(WrongActorRoleError);
    }
}
