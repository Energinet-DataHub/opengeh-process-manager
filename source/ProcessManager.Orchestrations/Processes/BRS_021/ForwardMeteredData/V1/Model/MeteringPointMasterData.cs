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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

public sealed record MeteringPointMasterData(
    MeteringPointId MeteringPointId,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    GridAreaCode GridAreaCode,
    ActorNumber GridAccessProvider,
    IReadOnlyCollection<string> NeighborGridAreaOwners,
    ConnectionState ConnectionState,
    MeteringPointType MeteringPointType,
    MeteringPointSubType MeteringPointSubType,
    Resolution Resolution,
    MeasurementUnit MeasurementUnit,
    string ProductId,
    MeteringPointId? ParentMeteringPointId,
    ActorNumber? EnergySupplier);

public record GridAreaCode(string Value);
