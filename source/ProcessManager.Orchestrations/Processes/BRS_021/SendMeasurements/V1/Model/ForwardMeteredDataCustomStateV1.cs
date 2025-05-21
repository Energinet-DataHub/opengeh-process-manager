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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Model;

public record ForwardMeteredDataCustomStateV1(
    IReadOnlyCollection<ForwardMeteredDataCustomStateV1.MasterData> HistoricalMeteringPointMasterData)
{
    public enum ConnectionState
    {
        NotUsed,
        ClosedDown,
        New,
        Connected,
        Disconnected,
    }

    public enum MeteringPointSubType // Målepunktsart, consider promoting this to a datahub ValueObject
    {
        Physical,
        Virtual,
        Calculated,
    }

    public record MasterData(
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
        ActorNumber? EnergySupplier)
    {
        public static MasterData? FromMeteringPointMasterData(MeteringPointMasterData? masterData)
        {
            if (masterData == null)
                return null;

            var connectionState = masterData.ConnectionState switch
            {
                Components.MeteringPointMasterData.Model.ConnectionState.NotUsed => ConnectionState.NotUsed,
                Components.MeteringPointMasterData.Model.ConnectionState.ClosedDown => ConnectionState.ClosedDown,
                Components.MeteringPointMasterData.Model.ConnectionState.New => ConnectionState.New,
                Components.MeteringPointMasterData.Model.ConnectionState.Connected => ConnectionState.Connected,
                Components.MeteringPointMasterData.Model.ConnectionState.Disconnected => ConnectionState.Disconnected,
                _ => throw new ArgumentOutOfRangeException(nameof(masterData.ConnectionState), masterData.ConnectionState, "Invalid connection state"),
            };

            var meteringPointSubType = masterData.MeteringPointSubType switch
            {
                Components.MeteringPointMasterData.Model.MeteringPointSubType.Physical => MeteringPointSubType.Physical,
                Components.MeteringPointMasterData.Model.MeteringPointSubType.Virtual => MeteringPointSubType.Virtual,
                Components.MeteringPointMasterData.Model.MeteringPointSubType.Calculated => MeteringPointSubType.Calculated,
                _ => throw new ArgumentOutOfRangeException(nameof(masterData.MeteringPointSubType), masterData.MeteringPointSubType, "Invalid metering point sub type"),
            };

            return new MasterData(
                MeteringPointId: masterData.MeteringPointId,
                ValidFrom: masterData.ValidFrom,
                ValidTo: masterData.ValidTo,
                GridAreaCode: new GridAreaCode(masterData.CurrentGridAreaCode.Value),
                GridAccessProvider: masterData.CurrentGridAccessProvider,
                NeighborGridAreaOwners: masterData.CurrentNeighborGridAreaOwners,
                ConnectionState: connectionState,
                MeteringPointType: masterData.MeteringPointType,
                MeteringPointSubType: meteringPointSubType,
                Resolution: masterData.Resolution,
                MeasurementUnit: masterData.MeasurementUnit,
                ProductId: masterData.ProductId,
                ParentMeteringPointId: masterData.ParentMeteringPointId,
                EnergySupplier: masterData.EnergySupplier);
        }

        public MeteringPointMasterData ToMeteringPointMasterData()
        {
            var connectionState = ConnectionState switch
            {
                ConnectionState.NotUsed => Components.MeteringPointMasterData.Model.ConnectionState.NotUsed,
                ConnectionState.ClosedDown => Components.MeteringPointMasterData.Model.ConnectionState.ClosedDown,
                ConnectionState.New => Components.MeteringPointMasterData.Model.ConnectionState.New,
                ConnectionState.Connected => Components.MeteringPointMasterData.Model.ConnectionState.Connected,
                ConnectionState.Disconnected => Components.MeteringPointMasterData.Model.ConnectionState.Disconnected,
                _ => throw new ArgumentOutOfRangeException(nameof(ConnectionState), ConnectionState, "Invalid connection state"),
            };

            var meteringPointSubType = MeteringPointSubType switch
            {
                MeteringPointSubType.Physical => Components.MeteringPointMasterData.Model.MeteringPointSubType.Physical,
                MeteringPointSubType.Virtual => Components.MeteringPointMasterData.Model.MeteringPointSubType.Virtual,
                MeteringPointSubType.Calculated => Components.MeteringPointMasterData.Model.MeteringPointSubType.Calculated,
                _ => throw new ArgumentOutOfRangeException(nameof(MeteringPointSubType), MeteringPointSubType, "Invalid metering point sub type"),
            };

            return new MeteringPointMasterData(
                MeteringPointId: MeteringPointId,
                ValidFrom: ValidFrom,
                ValidTo: ValidTo,
                CurrentGridAreaCode: new Components.MeteringPointMasterData.Model.GridAreaCode(GridAreaCode.Value),
                CurrentGridAccessProvider: GridAccessProvider,
                CurrentNeighborGridAreaOwners: NeighborGridAreaOwners,
                ConnectionState: connectionState,
                MeteringPointType: MeteringPointType,
                MeteringPointSubType: meteringPointSubType,
                Resolution: Resolution,
                MeasurementUnit: MeasurementUnit,
                ProductId: ProductId,
                ParentMeteringPointId: ParentMeteringPointId,
                EnergySupplier: EnergySupplier);
        }

        public ForwardMeteredDataCustomStateV2.MasterData ToV2()
        {
            var connectionState = ConnectionState switch
            {
                ConnectionState.NotUsed => ForwardMeteredDataCustomStateV2.ConnectionState.NotUsed,
                ConnectionState.ClosedDown => ForwardMeteredDataCustomStateV2.ConnectionState.ClosedDown,
                ConnectionState.New => ForwardMeteredDataCustomStateV2.ConnectionState.New,
                ConnectionState.Connected => ForwardMeteredDataCustomStateV2.ConnectionState.Connected,
                ConnectionState.Disconnected => ForwardMeteredDataCustomStateV2.ConnectionState.Disconnected,
                _ => throw new ArgumentOutOfRangeException(nameof(ConnectionState), ConnectionState, "Cannot convert connection state to v2."),
            };

            var meteringPointSubType = MeteringPointSubType switch
            {
                MeteringPointSubType.Physical => ForwardMeteredDataCustomStateV2.MeteringPointSubType.Physical,
                MeteringPointSubType.Virtual => ForwardMeteredDataCustomStateV2.MeteringPointSubType.Virtual,
                MeteringPointSubType.Calculated => ForwardMeteredDataCustomStateV2.MeteringPointSubType.Calculated,
                _ => throw new ArgumentOutOfRangeException(nameof(MeteringPointSubType), MeteringPointSubType, "Cannot convert metering point sub type to v2."),
            };

            return new ForwardMeteredDataCustomStateV2.MasterData(
                MeteringPointId,
                ValidFrom,
                ValidTo,
                new ForwardMeteredDataCustomStateV2.GridAreaCode(GridAreaCode.Value),
                GridAccessProvider,
                NeighborGridAreaOwners,
                connectionState,
                MeteringPointType,
                meteringPointSubType,
                Resolution,
                MeasurementUnit,
                ProductId,
                ParentMeteringPointId,
                EnergySupplier);
        }
    }

    public record GridAreaCode(string Value);
}
