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

using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Microsoft.Azure.Functions.Worker;
using NodaTime;
using NodaTime.Text;
using MeteringPointType = Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects.MeteringPointType;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

internal class FindReceiversActivity_Brs_021_ForwardMeteredData_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository,
    IElectricityMarketViews electricityMarketViews)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;

    [Function(nameof(FindReceiversActivity_Brs_021_ForwardMeteredData_V1))]
    public async Task<IReadOnlyCollection<Receiver>> Run(
        [ActivityTrigger] ActivityInput activityInput)
    {
        var orchestrationInstance = await ProgressRepository
            .GetAsync(activityInput.InstanceId)
            .ConfigureAwait(false);

        await TransitionStepToRunningAsync(
                Orchestration_Brs_021_ForwardMeteredData_V1.FindReceiverStep,
                orchestrationInstance)
            .ConfigureAwait(false);

        var receivers = await FindUniqueReceiversAsync(activityInput).ConfigureAwait(false);

        return receivers.AsReadOnly();
    }

    private static Receiver NeighborGridAccessProviderReceiver(ActorNumber neighborGridAccessProviderId)
    {
        return new Receiver(neighborGridAccessProviderId.Value, ActorRole.GridAccessProvider);
    }

    private static Receiver TheDanishEnergyAgencyReceiver()
    {
        return new Receiver("5798000020016", ActorRole.DanishEnergyAgency);
    }

    private static Receiver EnergySupplierReceiver(string energySupplierId)
    {
        return new Receiver(energySupplierId, ActorRole.EnergySupplier);
    }

    private async Task<IList<Receiver>> FindUniqueReceiversAsync(
        ActivityInput activityInput)
    {
        var receivers = new List<Receiver>();

        var meteringPointType = MeteringPointType.FromCode(activityInput.MeteringPointType);
        if (meteringPointType == MeteringPointType.Consumption || meteringPointType == MeteringPointType.Production)
        {
            var energySupplierReceivers =
                await GetEnergySuppliersForMeteringPointAsync(activityInput).ConfigureAwait(false);
            receivers.AddRange(energySupplierReceivers.Select(x => EnergySupplierReceiver(x.EnergySupplier.Value)));
            receivers.Add(TheDanishEnergyAgencyReceiver());
        }
        else if (meteringPointType == MeteringPointType.Exchange)
        {
            receivers.AddRange(activityInput.MeteringPointMasterData.NeighborGridAreaOwners.Select(NeighborGridAccessProviderReceiver));
        }
        else if (meteringPointType == MeteringPointType.VeProduction)
        {
            var parentEnergySupplierReceivers =
                await GetEnergySupplierForParentMeteringPointAsync(activityInput).ConfigureAwait(false);
            receivers.AddRange(
                parentEnergySupplierReceivers.Select(x => EnergySupplierReceiver(x.EnergySupplier.Value)));
            receivers.Add(TheDanishEnergyAgencyReceiver());
        }
        else if (meteringPointType == MeteringPointType.VeProduction
            || meteringPointType == MeteringPointType.NetProduction
            || meteringPointType == MeteringPointType.SupplyToGrid
            || meteringPointType == MeteringPointType.ConsumptionFromGrid
            || meteringPointType == MeteringPointType.WholesaleServicesInformation
            || meteringPointType == MeteringPointType.OwnProduction
            || meteringPointType == MeteringPointType.NetFromGrid
            || meteringPointType == MeteringPointType.NetToGrid
            || meteringPointType == MeteringPointType.TotalConsumption
            || meteringPointType == MeteringPointType.Analysis
            || meteringPointType == MeteringPointType.NotUsed
            || meteringPointType == MeteringPointType.SurplusProductionGroup6
            || meteringPointType == MeteringPointType.NetLossCorrection
            || meteringPointType == MeteringPointType.OtherConsumption
            || meteringPointType == MeteringPointType.OtherProduction
            || meteringPointType == MeteringPointType.ExchangeReactiveEnergy
            || meteringPointType == MeteringPointType.CollectiveNetProduction
            || meteringPointType == MeteringPointType.CollectiveNetConsumption)
        {
            // Child metering points should be sent to the energy supplier from the parent metering point
            var parentEnergySupplierReceivers2 =
                await GetEnergySupplierForParentMeteringPointAsync(activityInput).ConfigureAwait(false);
            receivers.AddRange(
                parentEnergySupplierReceivers2.Select(x => EnergySupplierReceiver(x.EnergySupplier.Value)));
        }

        var distinctReceivers = receivers
            .GroupBy(r => r.ActorId)
            .Select(g => g.First())
            .ToList();

        return distinctReceivers;
    }

    private async Task<IReadOnlyCollection<MeteringPointEnergySupplier>> GetEnergySupplierForParentMeteringPointAsync(
        ActivityInput activityInput)
    {
        var startDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.StartDateTime);
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.EndDateTime);
        return await _electricityMarketViews
            .GetMeteringPointEnergySuppliersAsync(
                activityInput.MeteringPointMasterData.ParentIdentification!,
                new Interval(startDateTime.Value, endDateTime.Value))
            .ToListAsync()
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<MeteringPointEnergySupplier>> GetEnergySuppliersForMeteringPointAsync(
        ActivityInput activityInput)
    {
        var startDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.StartDateTime);
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.EndDateTime);
        return await _electricityMarketViews
            .GetMeteringPointEnergySuppliersAsync(
                activityInput.MeteringPointMasterData.Identification!,
                new Interval(startDateTime.Value, endDateTime.Value))
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public sealed record ActivityInput(
        OrchestrationInstanceId InstanceId,
        string MeteringPointType,
        string StartDateTime,
        string EndDateTime,
        MeteringPointMasterData MeteringPointMasterData);
}
