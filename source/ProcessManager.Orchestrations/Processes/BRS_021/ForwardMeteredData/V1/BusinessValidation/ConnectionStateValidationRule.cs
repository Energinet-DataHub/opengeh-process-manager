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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class ConnectionStateValidationRule
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    public static IList<ValidationError> MeteringPointConnectionStateError => [new(
        Message: "Målepunktet skal have status tilsluttet eller afbrudt/meteringpoint must have status connected or disconnected",
        ErrorCode: "D16")];

    private static IList<ValidationError> NoError => [];

    public Task<IList<ValidationError>> ValidateAsync(
        ForwardMeteredDataBusinessValidatedDto subject)
    {
        if (subject.HistoricalMeteringPointMasterData
            .Any(x => x.ConnectionState != ConnectionState.Connected && x.ConnectionState != ConnectionState.Disconnected))
        {
            return Task.FromResult(MeteringPointConnectionStateError);
        }

        return Task.FromResult(NoError);
    }
}
