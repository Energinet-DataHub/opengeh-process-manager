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

using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ElectricityMarket.Integration.Models.Common;
using Energinet.DataHub.ElectricityMarket.Integration.Models.ProcessDelegation;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;

/// <summary>
/// Retrieves delegation information for a given actor number and role when receiving metering point data.
/// </summary>
/// <param name="electricityMarketViews"></param>
/// <remarks>
/// Has 3 possible outcomes:
/// 1. IsDelegated = false, ActorNumber = null -> No Delegation.
/// 2. IsDelegated = true, ActorNumber != null -> Delegation from ActorNumber.
/// 3. IsDelegated = true, ActorNumber = null -> Could not find expected delegation.
/// </remarks>
public class DelegationProvider(IElectricityMarketViews electricityMarketViews)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;

    /// <summary>
    /// Get the delegated actor which is delegated from.
    /// </summary>
    /// <param name="actorNumber">Actor number of the delegated to actor</param>
    /// <param name="actorRole">Actor role of the delegated to actor</param>
    /// <param name="gridAreaCode">The grid area code of the delegated from actor</param>
    /// <returns>
    ///     A boolean IsDelegated and ActorNumber of the delegated from actor if delegation exists.
    /// </returns>
    public async Task<(bool IsDelegated, string? ActorNumber)> GetDelegatedFromAsync(ActorNumber actorNumber, ActorRole? actorRole, GridAreaCode gridAreaCode)
    {
        if (actorRole != ActorRole.Delegated &&
            actorRole != ActorRole.GridAccessProvider)
        {
            // If the actor is not a Delegated or GridAccessProvider, we don't need to check for delegation
            return (false, null);
        }

        var delegation = await _electricityMarketViews.GetProcessDelegationAsync(
            actorNumber.Value,
            GetMarketRole(actorRole),
            gridAreaCode.Value,
            DelegatedProcess.ReceiveMeteringPointData).ConfigureAwait(false);

        var isDelegation = actorRole == ActorRole.Delegated
            || delegation is not null;

        return (isDelegation, delegation?.ActorNumber);
    }

    private static EicFunction GetMarketRole(ActorRole actorRole)
    {
        return actorRole switch
        {
            var currentActorRole when currentActorRole == ActorRole.GridAccessProvider => EicFunction.GridAccessProvider,
            var currentActorRole when currentActorRole == ActorRole.Delegated => EicFunction.Delegated,
            _ => throw new ArgumentOutOfRangeException(
                nameof(actorRole),
                actorRole,
                "Unknown EicFunction actor role value"),
        };
    }
}
