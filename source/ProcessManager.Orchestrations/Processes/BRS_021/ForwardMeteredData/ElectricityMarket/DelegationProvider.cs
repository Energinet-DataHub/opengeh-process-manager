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

public class DelegationProvider(IElectricityMarketViews electricityMarketViews)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;

    /// <summary>
    /// Checks if delegation is expected and gets the actor which is delegated from.
    /// </summary>
    /// <param name="gridAreaOwner">used to get the delegation as the delegated from actor</param>
    /// <param name="gridAreaCode">used to get the delegation</param>
    /// <param name="senderActorNumber">used to check against the found delegation, Actor number of the expected delegated to actor</param>
    /// <param name="senderActorRole">used to check for allow delegation roles and if delegation is expected, when role is Delegated</param>
    /// <returns>
    ///     A boolean ShouldBeDelegated, if delegation is expected and ActorNumber of the delegated from actor if delegation exists.
    /// </returns>
    public async Task<(bool ShouldBeDelegated, string? DelegatedFromActorNumber)> GetDelegatedFromAsync(ActorNumber gridAreaOwner, GridAreaCode gridAreaCode, ActorNumber senderActorNumber, ActorRole? senderActorRole)
    {
        if (senderActorRole != ActorRole.Delegated &&
            senderActorRole != ActorRole.GridAccessProvider)
        {
            // If the actor is not a Delegated or GridAccessProvider, we don't need to check for delegation
            return (false, null);
        }

        var delegation = await _electricityMarketViews.GetProcessDelegationAsync(
            gridAreaOwner.Value,
            EicFunction.GridAccessProvider,
            gridAreaCode.Value,
            DelegatedProcess.ReceiveMeteringPointData).ConfigureAwait(false);

        if (delegation?.ActorNumber == senderActorNumber.Value)
            return (true, gridAreaOwner.Value);

        var shouldBeDelegated = senderActorRole == ActorRole.Delegated;
        return (shouldBeDelegated, null);
    }
}
