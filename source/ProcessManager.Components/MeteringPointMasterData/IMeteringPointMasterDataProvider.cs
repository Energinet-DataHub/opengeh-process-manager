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

using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Extensions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;

public interface IMeteringPointMasterDataProvider
{
    /// <summary>
    /// Get a list of metering point master data for the given metering point id in the given period.
    /// <remarks>
    /// Returns an empty list if the given <paramref name="startDate"/> or <paramref name="endDate"/> cannot be parsed
    /// (using <see cref="InstantPatternWithOptionalSeconds"/>).
    /// </remarks>
    /// </summary>
    Task<IReadOnlyCollection<Model.MeteringPointMasterData>> GetMasterData(
        string meteringPointId,
        string startDate,
        string endDate);

    /// <summary>
    /// Get a list of metering point master data for the given metering point id in the given period.
    /// </summary>
    Task<IReadOnlyCollection<Model.MeteringPointMasterData>> GetMasterData(
        string meteringPointId,
        Instant startDateTime,
        Instant endDateTime);
}
