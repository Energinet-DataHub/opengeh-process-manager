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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;

public class MeteringPointReceiversProvider
{
    public StartForwardMeteredDataHandlerV1.ReceiversWithMeteredDataV1 GetReceiversFromMasterData(
        MeteringPointMasterData meteringPointMasterData)
    {
        var receivers = new List<MarketActorRecipientV1>();
        var meteringPointType = meteringPointMasterData.MeteringPointType;

        switch (meteringPointType)
        {
            case var _ when meteringPointType == MeteringPointType.Consumption:
            case var _ when meteringPointType == MeteringPointType.Production:
                receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                receivers.Add(TheDanishEnergyAgencyReceiver());
                break;
            case var _ when meteringPointType == MeteringPointType.Exchange:
                receivers.AddRange(
                    meteringPointMasterData.NeighborGridAreaOwners
                        .Select(ActorNumber.Create)
                        .Select(NeighborGridAccessProviderReceiver));
                break;
            case var _ when meteringPointType == MeteringPointType.VeProduction:
                receivers.Add(TheSystemOperatorReceiver());
                receivers.Add(TheDanishEnergyAgencyReceiver());
                // TODO: Add parent(s) as part of #607
                break;
            case var _ when meteringPointType == MeteringPointType.NetProduction:
            case var _ when meteringPointType == MeteringPointType.SupplyToGrid:
            case var _ when meteringPointType == MeteringPointType.ConsumptionFromGrid:
            case var _ when meteringPointType == MeteringPointType.WholesaleServicesInformation:
            case var _ when meteringPointType == MeteringPointType.OwnProduction:
            case var _ when meteringPointType == MeteringPointType.NetFromGrid:
            case var _ when meteringPointType == MeteringPointType.NetToGrid:
            case var _ when meteringPointType == MeteringPointType.TotalConsumption:
            case var _ when meteringPointType == MeteringPointType.Analysis:
            case var _ when meteringPointType == MeteringPointType.NotUsed:
            case var _ when meteringPointType == MeteringPointType.SurplusProductionGroup6:
            case var _ when meteringPointType == MeteringPointType.NetLossCorrection:
            case var _ when meteringPointType == MeteringPointType.OtherConsumption:
            case var _ when meteringPointType == MeteringPointType.OtherProduction:
            case var _ when meteringPointType == MeteringPointType.ExchangeReactiveEnergy:
            case var _ when meteringPointType == MeteringPointType.CollectiveNetProduction:
            case var _ when meteringPointType == MeteringPointType.CollectiveNetConsumption:
                if (meteringPointMasterData.ParentMeteringPointId != null)
                {
                    // TODO: This part will be impl as part of #607
                    // var parentEnergySuppliers =
                    //     await GetEnergySupplierForMeteringPointAsync(
                    //             activityInput.MeteringPointMasterData.ParentMeteringPointId,
                    //             activityInput.StartDateTime,
                    //             activityInput.EndDateTime)
                    //         .ConfigureAwait(false);
                    // receivers.AddRange(parentEnergySuppliers.Select(x => EnergySupplierReceiver(x.EnergySupplier)));
                }
                else
                {
                    // TODO: NO-OP or should we throw an exception?
                }

                break;
            default:
                throw new NotImplementedException();
        }

        var distinctReceivers = receivers
            .DistinctBy(r => r.ActorNumber)
            .ToList();

        return new StartForwardMeteredDataHandlerV1.ReceiversWithMeteredDataV1(
            distinctReceivers,
            meteringPointMasterData.Resolution,
            meteringPointMasterData.MeasurementUnit,
            meteringPointMasterData.ValidFrom,
            meteringPointMasterData.ValidTo,
            []);
    }

    private static MarketActorRecipientV1
        NeighborGridAccessProviderReceiver(ActorNumber neighborGridAccessProviderId) => new(
        neighborGridAccessProviderId,
        ActorRole.GridAccessProvider);

    private static MarketActorRecipientV1 TheDanishEnergyAgencyReceiver() => new(
        ActorNumber.Create(DataHubDetails.DanishEnergyAgencyNumber),
        ActorRole.DanishEnergyAgency);

    private static MarketActorRecipientV1 TheSystemOperatorReceiver() => new(
        ActorNumber.Create(DataHubDetails.SystemOperatorNumber),
        ActorRole.SystemOperator);

    private static MarketActorRecipientV1 EnergySupplierReceiver(ActorNumber energySupplierId) =>
        new(energySupplierId, ActorRole.EnergySupplier);
}
