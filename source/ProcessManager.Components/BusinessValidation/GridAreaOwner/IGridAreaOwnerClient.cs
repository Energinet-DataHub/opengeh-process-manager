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

namespace Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;

/// <summary>
/// Placeholder / mock until we can get grid area owners from Market Participant.
/// TODO: Replace with market participant client.
/// </summary>
public interface IGridAreaOwnerClient
{
    // TODO: Remove or reintroduce GetCurrentOwnerAsync() implementation instead of IsCurrentOwnerAsync()
    // Task<GridAreaOwner?> GetCurrentOwnerAsync(string gridArea, CancellationToken cancellationToken);
    // public record GridAreaOwner(
    //     Guid Id,
    //     string GridAreaCode,
    //     string OwnerActorNumber,
    //     Instant ValidFrom,
    //     int SequenceNumber);

    /// <summary>
    /// Check if the given <paramref name="actorNumber"/> is the current owner of the given <paramref name="gridArea"/>.
    /// </summary>
    /// <returns>Returns true of the given <paramref name="actorNumber"/> is the current owner.</returns>
    Task<bool> IsCurrentOwnerAsync(string gridArea, string actorNumber, CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of <see cref="IGridAreaOwnerClient"/> that uses <see cref="IElectricityMarketViews"/> to get the grid area owner.
/// </summary>
public class GridAreaOwnerClient(
    IElectricityMarketViews electricityMarketViews)
    : IGridAreaOwnerClient
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;

    public async Task<bool> IsCurrentOwnerAsync(string gridArea, string actorNumber, CancellationToken cancellationToken)
    {
        var gridAreaOwner = await _electricityMarketViews.GetGridAreaOwnerAsync(gridArea).ConfigureAwait(false);

        if (gridAreaOwner == null)
            return false;

        return gridAreaOwner.GridAccessProviderGln.Equals(actorNumber);
    }
}
