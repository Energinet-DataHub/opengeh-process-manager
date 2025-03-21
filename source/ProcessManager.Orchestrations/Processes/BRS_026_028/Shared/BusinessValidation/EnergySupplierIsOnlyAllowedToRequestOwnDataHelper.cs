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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.Shared.BusinessValidation;

public static class EnergySupplierIsOnlyAllowedToRequestOwnDataHelper
{
    private static readonly ValidationError _invalidEnergySupplierField = new("Feltet EnergySupplier skal være udfyldt med et valid GLN/EIC nummer når en elleverandør anmoder om data / EnergySupplier must be submitted with a valid GLN/EIC number when an energy supplier requests data", "E16");
    private static readonly ValidationError _notEqualToRequestedBy = new("Elleverandør i besked stemmer ikke overenes med elleverandør i header / Energy supplier in message does not correspond with energy supplier in header", "E16");

    private static IList<ValidationError> NoError => [];

    private static IList<ValidationError> InvalidEnergySupplierError => [_invalidEnergySupplierField];

    private static IList<ValidationError> NotEqualToRequestedByError => [_notEqualToRequestedBy];

    public static Task<IList<ValidationError>> ValidateAsync(string requestedForActorRole, string requestedForActorNumber, string? energySupplierNumber)
    {
        if (requestedForActorRole != ActorRole.EnergySupplier.Name)
             return Task.FromResult(NoError);

        if (string.IsNullOrEmpty(energySupplierNumber))
            return Task.FromResult(InvalidEnergySupplierError);

        if (!IsValidEnergySupplierIdFormat(energySupplierNumber))
            return Task.FromResult(InvalidEnergySupplierError);

        if (!RequestedForActorNumberEqualsEnergySupplier(requestedForActorNumber, energySupplierNumber))
            return Task.FromResult(NotEqualToRequestedByError);

        return Task.FromResult(NoError);
    }

    private static bool IsValidEnergySupplierIdFormat(string energySupplierId)
    {
        return
            ActorNumberValidator.IsValidGlnNumber(energySupplierId)
            || ActorNumberValidator.IsValidEicNumber(energySupplierId);
    }

    private static bool RequestedForActorNumberEqualsEnergySupplier(string requestedForActorNumber, string energySupplierNumber)
    {
        return requestedForActorNumber.Equals(energySupplierNumber, StringComparison.OrdinalIgnoreCase);
    }
}
