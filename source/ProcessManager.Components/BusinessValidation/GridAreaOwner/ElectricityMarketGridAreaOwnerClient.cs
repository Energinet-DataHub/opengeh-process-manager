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
using Energinet.DataHub.ElectricityMarket.Integration.Models.GridAreas;
using Energinet.DataHub.ElectricityMarket.Integration.Options;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;

/// <summary>
/// Implementation of <see cref="IGridAreaOwnerClient"/> that uses <see cref="IElectricityMarketViews"/> to get the grid area owner.
/// <remarks>
/// Requires <see cref="ElectricityMarketClientOptions"/> to be registered in app settings.
/// </remarks>
/// </summary>
public class ElectricityMarketGridAreaOwnerClient(
    IElectricityMarketViews electricityMarketViews,
    ILogger<ElectricityMarketGridAreaOwnerClient> logger)
    : IGridAreaOwnerClient
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;
    private readonly ILogger<ElectricityMarketGridAreaOwnerClient> _logger = logger;

    public async Task<bool> IsCurrentOwnerAsync(string gridArea, string actorNumber, CancellationToken cancellationToken)
    {
        GridAreaOwnerDto? gridAreaOwner;
        try
        {
            gridAreaOwner = await _electricityMarketViews.GetGridAreaOwnerAsync(gridArea).ConfigureAwait(false);
        }
        catch (HttpRequestException httpRequestException)
        {
            // If a grid area owner isn't found, Electricity Market returns a 500 Internal Server Error, which causes
            // a HttpRequestException to be thrown. We log this exception and return false until the issue is fixed.
            _logger.LogError(
                exception: httpRequestException,
                "Http request exception while getting grid area owner for grid area '{GridArea}'",
                gridArea);
            gridAreaOwner = null;
        }
        catch (Exception e)
        {
            throw new Exception($"Exception while getting grid area owner for grid area '{gridArea}'", e);
        }

        // Grid area owner wasn't found, so the actor cannot be the grid area owner.
        if (gridAreaOwner == null)
            return false;

        return gridAreaOwner.GridAccessProviderGln.Equals(actorNumber);
    }
}
