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
/// Mock of the <see cref="IGridAreaOwnerClient"/>, that always returns true.
/// </summary>
public class GridAreaOwnerMockClient : IGridAreaOwnerClient
{
    public Task<bool> IsCurrentOwnerAsync(string gridArea, string actorNumber, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}
