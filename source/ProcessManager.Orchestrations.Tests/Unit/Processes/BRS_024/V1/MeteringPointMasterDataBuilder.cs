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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Extensions;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using NodaTime;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_024.V1;

public class MeteringPointMasterDataBuilder
{
    public MeteringPointMasterData BuildFromInput(
        RequestYearlyMeasurementsInputV1 input,
        MeteringPointSubType? meteringPointSubType = null,
        MeasurementUnit? measurementUnit = null,
        string? gridAccessProvider = null,
        MeteringPointType? meteringPointType = null,
        ConnectionState? connectionState = null)
    {
        var end = InstantPatternWithOptionalSeconds.Parse(input.ReceivedAt).Value;
        var start = end.Minus(Duration.FromDays(365));

        return new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            CurrentGridAreaCode: new GridAreaCode("804"),
            CurrentGridAccessProvider: gridAccessProvider != null ? ActorNumber.Create(gridAccessProvider) : ActorNumber.Create("1111111111111"),
            ConnectionState: connectionState ?? ConnectionState.Connected,
            MeteringPointType: meteringPointType ?? MeteringPointType.Production,
            MeteringPointSubType: meteringPointSubType ?? MeteringPointSubType.Physical,
            MeasurementUnit: measurementUnit ?? MeasurementUnit.KilowattHour,
            ValidFrom: start.ToDateTimeOffset(),
            ValidTo: end.ToDateTimeOffset(),
            CurrentNeighborGridAreaOwners: [],
            Resolution: Resolution.QuarterHourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
    }
}
