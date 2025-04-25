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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1;

public class MeteringPointMasterDataBuilder
{
    public MeteringPointMasterData BuildFromInput(
        ForwardMeteredDataInputV1 input,
        MeteringPointSubType? meteringPointSubType = null,
        MeasurementUnit? measurementUnit = null,
        string? gridAccessProvider = null,
        MeteringPointType? meteringPointType = null,
        ConnectionState? connectionState = null,
        string? endDateTime = null)
    {
        var start = InstantPatternWithOptionalSeconds.Parse(input.StartDateTime).Value;
        var end = InstantPatternWithOptionalSeconds.Parse(endDateTime ?? input.EndDateTime!).Value;

        return new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: gridAccessProvider != null ? ActorNumber.Create(gridAccessProvider) : ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: connectionState ?? ConnectionState.Connected,
            MeteringPointType: meteringPointType ?? MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: meteringPointSubType ?? MeteringPointSubType.Physical,
            MeasurementUnit: measurementUnit ?? MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: start.ToDateTimeOffset(),
            ValidTo: end.ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.FromName(input.Resolution!),
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
    }
}
