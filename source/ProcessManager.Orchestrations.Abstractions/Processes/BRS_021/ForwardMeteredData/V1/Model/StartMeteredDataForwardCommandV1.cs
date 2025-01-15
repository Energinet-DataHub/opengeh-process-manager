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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

/// <summary>
/// Command for starting a BRS-021-ForwardMeteredData.
/// Must be JSON serializable.
/// </summary>
public record StartForwardMeteredDataCommandV1
        : MessageCommand<MeteredDataForMeteringPointMessageInputV1>
{
    /// <summary>
    /// Construct command.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the command.</param>
    /// <param name="inputParameter">Contains the Durable Functions orchestration input parameter value.</param>
    /// <param name="idempotencyKey">
    /// A value used by the Process Manager to ensure idempotency for a message command.
    /// The creator of the command must create a key that is unique per command.</param>
    public StartForwardMeteredDataCommandV1(
        ActorIdentityDto operatingIdentity,
        MeteredDataForMeteringPointMessageInputV1 inputParameter,
        string idempotencyKey)
            : base(
                operatingIdentity,
                orchestrationDescriptionUniqueName: new Brs_021_ForwardedMeteredData_V1(),
                inputParameter,
                idempotencyKey)
    {
    }
}
