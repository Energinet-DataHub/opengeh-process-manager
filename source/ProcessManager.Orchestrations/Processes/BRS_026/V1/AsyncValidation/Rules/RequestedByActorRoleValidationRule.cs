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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.AsyncValidation.Rules;

public sealed class RequestedByActorRoleValidationRule : IValidationRule<RequestCalculatedEnergyTimeSeriesInputV1>
{
    private static IList<ValidationError> NoError => [];

    private static IList<ValidationError> UseMeteredDataResponsibleInsteadOfGridAreaOperatorError => [new(
        "Rollen skal være MDR når der anmodes om beregnede energitidsserier / Role must be MDR when requesting aggregated measure data",
        "D02")];

    public Task<IList<ValidationError>> ValidateAsync(RequestCalculatedEnergyTimeSeriesInputV1 subject)
    {
        if (subject.RequestedForActorRole == ActorRole.GridAccessProvider.Name)
            return Task.FromResult(UseMeteredDataResponsibleInsteadOfGridAreaOperatorError);

        return Task.FromResult(NoError);
    }
}
