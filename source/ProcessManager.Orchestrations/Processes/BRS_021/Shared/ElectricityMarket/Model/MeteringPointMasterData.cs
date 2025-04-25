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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;

/// <summary>
/// MeteringPointMasterData is a record that contains the master data for a metering point. For a given period, the master data is valid from ValidFrom to ValidTo.
/// </summary>
/// <param name="MeteringPointId">The ID of the MeteringPoint.</param>
/// <param name="ValidFrom">The start of the period is inclusive.</param>
/// <param name="ValidTo">The end of the period is exclusive.</param>
/// <param name="CurrentGridAreaCode">The current grid area code, even tough the request is for a different period.</param>
/// <param name="CurrentGridAccessProvider">The current grid access provider, even tough the request is for a different period.</param>
/// <param name="CurrentNeighborGridAreaOwners">The current neighboring grid area owners, even tough the request is for a different period.</param>
/// <param name="ConnectionState">The connection state of the metering point.</param>
/// <param name="MeteringPointType">The type of metering point the request is for.</param>
/// <param name="MeteringPointSubType">The subtype of metering point the request is for.</param>
/// <param name="Resolution">The resolution of the metering point.</param>
/// <param name="MeasurementUnit">The measurement unit of the metering point.</param>
/// <param name="ProductId">The id of the product on the metering point id.</param>
/// <param name="ParentMeteringPointId">The ID for the parent metering point which the master data have been retrieved, to access information about the EnergySupplier, the master data for the parent must be retrieved.</param>
/// <param name="EnergySupplier">The ID of the energy supplier.</param>
public sealed record MeteringPointMasterData(
    MeteringPointId MeteringPointId,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    GridAreaCode CurrentGridAreaCode,
    ActorNumber CurrentGridAccessProvider,
    IReadOnlyCollection<string> CurrentNeighborGridAreaOwners,
    ConnectionState ConnectionState,
    MeteringPointType MeteringPointType,
    MeteringPointSubType MeteringPointSubType,
    Resolution Resolution,
    MeasurementUnit MeasurementUnit,
    string ProductId,
    MeteringPointId? ParentMeteringPointId,
    ActorNumber? EnergySupplier);
