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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;

/// <summary>
/// Command for starting a BRS-021 electrical heating calculation.
/// Must be JSON serializable.
/// </summary>
public sealed record StartElectricalHeatingCalculationCommandV1
    : StartOrchestrationInstanceCommand<UserIdentityDto>
{
    /// <summary>
    /// Construct command.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the command.</param>
    public StartElectricalHeatingCalculationCommandV1(
        UserIdentityDto operatingIdentity)
            : base(
                operatingIdentity,
                orchestrationDescriptionUniqueName: new Brs_021_ElectricalHeatingCalculation_V1())
    {
    }
}
