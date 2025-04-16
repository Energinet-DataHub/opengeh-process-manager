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

public record ForwardMeteredDataCustomStateV2(
    ForwardMeteredDataCustomStateV2.MasterData? CurrentMeteringPointMasterData,
    IReadOnlyCollection<ForwardMeteredDataCustomStateV2.MasterData> HistoricalMeteringPointMasterData)
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
        public static MasterData? TryFromMeteringPointMasterData(Shared.ElectricityMarket.Model.MeteringPointMasterData? masterData)
        {
            return masterData is not null
                ? FromMeteringPointMasterData(masterData)
                : null;
        }

        public static MasterData FromMeteringPointMasterData(Shared.ElectricityMarket.Model.MeteringPointMasterData masterData)
        {
            var connectionState = masterData.ConnectionState switch
            {
                Shared.ElectricityMarket.Model.ConnectionState.NotUsed => ConnectionState.NotUsed,
                Shared.ElectricityMarket.Model.ConnectionState.ClosedDown => ConnectionState.ClosedDown,
                Shared.ElectricityMarket.Model.ConnectionState.New => ConnectionState.New,
                Shared.ElectricityMarket.Model.ConnectionState.Connected => ConnectionState.Connected,
                Shared.ElectricityMarket.Model.ConnectionState.Disconnected => ConnectionState.Disconnected,
                _ => throw new ArgumentOutOfRangeException(nameof(masterData.ConnectionState), masterData.ConnectionState, "Invalid connection state"),
            };

            var meteringPointSubType = masterData.MeteringPointSubType switch
            {
                Shared.ElectricityMarket.Model.MeteringPointSubType.Physical => MeteringPointSubType.Physical,
                Shared.ElectricityMarket.Model.MeteringPointSubType.Virtual => MeteringPointSubType.Virtual,
                Shared.ElectricityMarket.Model.MeteringPointSubType.Calculated => MeteringPointSubType.Calculated,
                _ => throw new ArgumentOutOfRangeException(nameof(masterData.MeteringPointSubType), masterData.MeteringPointSubType, "Invalid metering point sub type"),
            };

            return new MasterData(
                MeteringPointId: masterData.MeteringPointId,
                ValidFrom: masterData.ValidFrom,
                ValidTo: masterData.ValidTo,
                GridAreaCode: new GridAreaCode(masterData.GridAreaCode.Value),
                GridAccessProvider: masterData.GridAccessProvider,
                NeighborGridAreaOwners: masterData.NeighborGridAreaOwners,
                ConnectionState: connectionState,
                MeteringPointType: masterData.MeteringPointType,
                MeteringPointSubType: meteringPointSubType,
                Resolution: masterData.Resolution,
                MeasurementUnit: masterData.MeasurementUnit,
                ProductId: masterData.ProductId,
                ParentMeteringPointId: masterData.ParentMeteringPointId,
                EnergySupplier: masterData.EnergySupplier);
        }

        public static IReadOnlyCollection<MasterData> FromMeteringPointMasterData(
            IEnumerable<Shared.ElectricityMarket.Model.MeteringPointMasterData> masterData)
        {
            return masterData.Select(FromMeteringPointMasterData).ToList();
        }

        public Shared.ElectricityMarket.Model.MeteringPointMasterData ToMeteringPointMasterData()
        {
            var connectionState = ConnectionState switch
            {
                ConnectionState.NotUsed => Shared.ElectricityMarket.Model.ConnectionState.NotUsed,
                ConnectionState.ClosedDown => Shared.ElectricityMarket.Model.ConnectionState.ClosedDown,
                ConnectionState.New => Shared.ElectricityMarket.Model.ConnectionState.New,
                ConnectionState.Connected => Shared.ElectricityMarket.Model.ConnectionState.Connected,
                ConnectionState.Disconnected => Shared.ElectricityMarket.Model.ConnectionState.Disconnected,
                _ => throw new ArgumentOutOfRangeException(nameof(ConnectionState), ConnectionState, "Invalid connection state"),
            };

            var meteringPointSubType = MeteringPointSubType switch
            {
                MeteringPointSubType.Physical => Shared.ElectricityMarket.Model.MeteringPointSubType.Physical,
                MeteringPointSubType.Virtual => Shared.ElectricityMarket.Model.MeteringPointSubType.Virtual,
                MeteringPointSubType.Calculated => Shared.ElectricityMarket.Model.MeteringPointSubType.Calculated,
                _ => throw new ArgumentOutOfRangeException(nameof(MeteringPointSubType), MeteringPointSubType, "Invalid metering point sub type"),
            };

            return new Shared.ElectricityMarket.Model.MeteringPointMasterData(
                MeteringPointId: MeteringPointId,
                ValidFrom: ValidFrom,
                ValidTo: ValidTo,
                GridAreaCode: new Shared.ElectricityMarket.Model.GridAreaCode(GridAreaCode.Value),
                GridAccessProvider: GridAccessProvider,
                NeighborGridAreaOwners: NeighborGridAreaOwners,
                ConnectionState: connectionState,
                MeteringPointType: MeteringPointType,
                MeteringPointSubType: meteringPointSubType,
                Resolution: Resolution,
                MeasurementUnit: MeasurementUnit,
                ProductId: ProductId,
                ParentMeteringPointId: ParentMeteringPointId,
                EnergySupplier: EnergySupplier);
        }
    }

    public record GridAreaCode(string Value);
}
