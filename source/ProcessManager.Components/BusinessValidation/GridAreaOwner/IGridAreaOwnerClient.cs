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

namespace Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;

/// <summary>
/// A wrapper communication with ElectricityMarket, that can check if a given actor is the current owner of a grid area.
/// </summary>
public interface IGridAreaOwnerClient
{
    /// <summary>
    /// Check if the given <paramref name="actorNumber"/> is the current owner of the given <paramref name="gridArea"/>.
    /// </summary>
    /// <returns>Returns true of the given <paramref name="actorNumber"/> is the current owner.</returns>
    Task<bool> IsCurrentOwnerAsync(string gridArea, string actorNumber, CancellationToken cancellationToken);
}
