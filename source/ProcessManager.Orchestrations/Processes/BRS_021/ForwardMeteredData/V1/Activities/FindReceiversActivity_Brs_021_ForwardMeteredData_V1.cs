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
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Components.DataHub.Measurements.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Microsoft.Azure.Functions.Worker;
using NodaTime;
using MeteringPointType = Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType;

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

    /// <summary>
    /// Responsible for finding the unique receivers based on the metering point type.
    /// </summary>
    /// <returns>
    /// Returns a ActivityOutput with read-only list of MarketActorRecipients.
    /// Returns empty list if no receivers are found.
    /// </returns>
    [Function(nameof(FindReceiversActivity_Brs_021_ForwardMeteredData_V1))]
    public async Task<ActivityOutput> Run(
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

        return new ActivityOutput(
            MarketActorRecipients: receivers.AsReadOnly());
    }

    private static MarketActorRecipient NeighborGridAccessProviderReceiver(ActorNumber neighborGridAccessProviderId)
    {
        return new MarketActorRecipient(neighborGridAccessProviderId.Value, ActorRole.GridAccessProvider);
    }

    private static MarketActorRecipient TheDanishEnergyAgencyReceiver()
    {
        return new MarketActorRecipient(DataHubDetails.DanishEnergyAgencyNumber, ActorRole.DanishEnergyAgency);
    }

    private static MarketActorRecipient TheSystemOperatorReceiver()
    {
        return new MarketActorRecipient(DataHubDetails.SystemOperatorNumber, ActorRole.SystemOperator);
    }

    private static MarketActorRecipient EnergySupplierReceiver(ActorNumber energySupplierId)
    {
        return new MarketActorRecipient(energySupplierId.Value, ActorRole.EnergySupplier);
    }

    private async Task<IList<MarketActorRecipient>> FindUniqueReceiversAsync(
        ActivityInput activityInput)
    {
        var receivers = new List<MarketActorRecipient>();

        var meteringPointType = MeteringPointType.FromCode(activityInput.MeteringPointType);
        if (meteringPointType == MeteringPointType.Consumption || meteringPointType == MeteringPointType.Production)
        {
            var energySuppliers =
                await GetEnergySuppliersForMeteringPointAsync(activityInput).ConfigureAwait(false);
            receivers.AddRange(energySuppliers.Select(x => EnergySupplierReceiver(x.EnergySupplier)));
            receivers.Add(TheDanishEnergyAgencyReceiver());
        }
        else if (meteringPointType == MeteringPointType.Exchange)
        {
            receivers.AddRange(activityInput.MeteringPointMasterData.NeighborGridAreaOwners.Select(NeighborGridAccessProviderReceiver));
        }
        else if (meteringPointType == MeteringPointType.VeProduction)
        {
            var parentEnergySuppliers =
                await GetEnergySupplierFromParentMeteringPointAsync(activityInput).ConfigureAwait(false);
            receivers.AddRange(parentEnergySuppliers.Select(x => EnergySupplierReceiver(x.EnergySupplier)));
            receivers.Add(TheDanishEnergyAgencyReceiver());
            receivers.Add(TheSystemOperatorReceiver());
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
            var parentEnergySuppliers =
                await GetEnergySupplierFromParentMeteringPointAsync(activityInput).ConfigureAwait(false);
            receivers.AddRange(parentEnergySuppliers.Select(x => EnergySupplierReceiver(x.EnergySupplier)));
        }

        var distinctReceivers = receivers
            .GroupBy(r => r.ActorId)
            .Select(g => g.First())
            .ToList();

        return distinctReceivers;
    }

    private async Task<IReadOnlyCollection<MeteringPointEnergySupplier>> GetEnergySupplierFromParentMeteringPointAsync(
        ActivityInput activityInput)
    {
        var startDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.StartDateTime);
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.EndDateTime);

        if (activityInput.MeteringPointMasterData.ParentIdentification == null)
            return new List<MeteringPointEnergySupplier>();

        return await _electricityMarketViews
            .GetMeteringPointEnergySuppliersAsync(
                activityInput.MeteringPointMasterData.ParentIdentification,
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

    public sealed record ActivityOutput(IReadOnlyCollection<MarketActorRecipient> MarketActorRecipients);
}
