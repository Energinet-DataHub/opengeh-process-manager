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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using NodaTime;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;

[SuppressMessage(
    "StyleCop.CSharp.ReadabilityRules",
    "SA1118:Parameter should not span multiple lines",
    Justification = "Readability")]
public class MeteringPointReceiversProvider(
    DateTimeZone dateTimeZone)
{
    private static readonly IReadOnlyDictionary<Resolution, Duration> _resolutionToDurationMap = new Dictionary<Resolution, Duration>
    {
        { Resolution.QuarterHourly, Duration.FromMinutes(15) },
        { Resolution.Hourly, Duration.FromHours(1) },
        { Resolution.Daily, Duration.FromDays(1) },
    };

    private readonly DateTimeZone _dateTimeZone = dateTimeZone;

    public List<ReceiversWithMeasureData> GetReceiversWithMeteredDataFromMasterDataList(
        FindReceiversInput input)
    {
        if (input.MasterData.Count == 0)
            throw new InvalidOperationException($"The metering point master data list is empty (MeteringPointId={input.MeteringPointId}, StartDateTime={input.StartDateTime}, EndDateTime={input.EndDateTime})");

        // Ensure metered data is sorted by position
        var sortedMeteredData = new SortedDictionary<int, ReceiversWithMeasureData.MeasureData>(
            input.MeasureData.ToDictionary(md => md.Position));

        // Ensure master data is sorted by ValidFrom
        var masterDataDictionary = input.MasterData.ToDictionary(mpmd => mpmd.ValidFrom.ToInstant());

        var allReceivers = CalculateReceiversWithMeteredDataForMasterDataPeriods(
            input.StartDateTime,
            input.EndDateTime,
            input.Resolution, // Resolution shouldn't change between master data periods, else validation should fail
            masterDataDictionary,
            sortedMeteredData);

        return allReceivers;
    }

    /// <summary>
    /// Split metered data into periods based on the master data periods.
    /// <remarks>
    /// This method DOES NOT support different resolutions for each master data period. However, business
    /// validation should ensure that the resolution is the same for each master data period (in the same transaction).
    /// </remarks>
    /// </summary>
    private List<ReceiversWithMeasureData> CalculateReceiversWithMeteredDataForMasterDataPeriods(
        Instant inputPeriodStart,
        Instant inputPeriodEnd,
        Resolution resolution,
        Dictionary<Instant, MeteringPointMasterData> masterData,
        SortedDictionary<int, ReceiversWithMeasureData.MeasureData> sortedMeteredData)
    {
        if (masterData.Count == 1)
        {
            // If there is only one master data element then we can skip a lot of logic, since we don't
            // need to split the metered data into periods and recalculate positions
            var masterDataElement = masterData.Values.Single();

            var receiversWithMeteredData = CreateReceiversWithMeteredData(
                new MasterDataWithMeteredData(
                    masterDataElement,
                    inputPeriodStart,
                    inputPeriodEnd,
                    [.. sortedMeteredData.Values]));

            return [receiversWithMeteredData];
        }

        var currentTimestamp = inputPeriodStart;

        var firstMasterData = masterData.Values.First();
        var currentMasterData = new MasterDataWithMeteredData(firstMasterData, inputPeriodStart, inputPeriodEnd, []);

        List<MasterDataWithMeteredData> masterDataWithMeteredDataList = [currentMasterData];

        foreach (var meteredData in sortedMeteredData.Values)
        {
            // If current timestamp is equal to (or later than) the total period, throw an exception. This assumes totalPeriodEnd is exclusive.
            if (currentTimestamp >= inputPeriodEnd)
                throw new InvalidOperationException($"The current timestamp is after the metered data period (Position={meteredData.Position}, CurrentTimestamp={currentTimestamp}, PeriodEnd={inputPeriodEnd})");

            // Get master data for current timestamp. This assumes ValidTo is exclusive.
            var currentTimestampBelongsToNextMasterDataPeriod = currentTimestamp >= currentMasterData.ValidTo;
            if (currentTimestampBelongsToNextMasterDataPeriod)
            {
                // The master data should always be continuous (with no overlaps), so if this fails then
                // the master data (or our implementation) has a bug.
                if (!masterData.TryGetValue(currentTimestamp, out var nextMasterData))
                    throw new InvalidOperationException($"The master data for the current timestamp is missing (MeteringPointId={currentMasterData.MasterData.MeteringPointId.Value}, Position={meteredData.Position}, CurrentTimestamp={currentTimestamp})");

                currentMasterData = new MasterDataWithMeteredData(nextMasterData, inputPeriodStart, inputPeriodEnd, []);

                // Add the new master data with metered data to the list
                masterDataWithMeteredDataList.Add(currentMasterData);
            }

            // These safeguards shouldn't be reached if the master data (and our implementation) is correct,
            // so if the performance is critical then these checks can be removed.
            if (currentTimestamp < currentMasterData.ValidFrom)
                throw new InvalidOperationException($"The current timestamp is before the master data period start (MeteringPointId={currentMasterData.MasterData.MeteringPointId.Value}, Position={meteredData.Position}, CurrentTimestamp={currentTimestamp}, MasterDataValidFrom={currentMasterData.ValidFrom})");

            if (currentTimestamp >= currentMasterData.ValidTo)
                throw new InvalidOperationException($"The current timestamp is equal to or after the master data period end (MeteringPointId={currentMasterData.MasterData.MeteringPointId.Value}, Position={meteredData.Position}, CurrentTimestamp={currentTimestamp}, MasterDataValidTo={currentMasterData.ValidTo})");

            // Position is 1-indexed, so the new position is the current count + 1 (if the list is empty, the new position should be 1)
            var newPosition = currentMasterData.MeasureData.Count + 1;
            currentMasterData.MeasureData.Add(meteredData with { Position = newPosition });

            // Get next timestamp
            currentTimestamp = AddResolutionToTimestamp(currentTimestamp, resolution);
        }

        return masterDataWithMeteredDataList
            .Select(CreateReceiversWithMeteredData)
            .ToList();
    }

    private Instant AddResolutionToTimestamp(Instant timestamp, Resolution currentResolution)
    {
        // Special case for monthly resolution, since month are dependent on the calendar instead of
        // having a fixed length (e.g. 28, 29, 30 or 31 days)
        if (currentResolution == Resolution.Monthly)
        {
            var asZonedDateTime = new ZonedDateTime(timestamp, _dateTimeZone);

            var nextDateTime = asZonedDateTime.LocalDateTime.PlusMonths(1);

            return nextDateTime.InZoneStrictly(_dateTimeZone).ToInstant();
        }

        if (!_resolutionToDurationMap.TryGetValue(currentResolution, out var durationForCurrentResolution))
            throw new ArgumentOutOfRangeException(nameof(currentResolution), currentResolution.Name, "The resolution is not supported");

        // Resolution should now be below a month, so we can use math directly on the instant (the resolution
        // is a fixed amount of time)
        var nextTimestamp = timestamp.Plus(durationForCurrentResolution);
        return nextTimestamp;
    }

    private ReceiversWithMeasureData CreateReceiversWithMeteredData(
        MasterDataWithMeteredData masterDataWithMeteredData)
    {
        var actorReceivers = GetReceiversFromMasterData(masterDataWithMeteredData.MasterData);

        return new ReceiversWithMeasureData(
            Receivers: actorReceivers,
            Resolution: masterDataWithMeteredData.MasterData.Resolution,
            MeasureUnit: masterDataWithMeteredData.MasterData.MeasurementUnit,
            StartDateTime: masterDataWithMeteredData.ValidFrom.ToDateTimeOffset(),
            EndDateTime: masterDataWithMeteredData.ValidTo.ToDateTimeOffset(),
            MeasureDataList: masterDataWithMeteredData.MeasureData);
    }

    private List<Actor> GetReceiversFromMasterData(
        MeteringPointMasterData meteringPointMasterData)
    {
        var receivers = new List<Actor>();
        var meteringPointType = meteringPointMasterData.MeteringPointType;

        switch (meteringPointType)
        {
            case var _ when meteringPointType == MeteringPointType.Consumption:
                if (meteringPointMasterData.EnergySupplier is not null)
                {
                    receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                }

                break;
            case var _ when meteringPointType == MeteringPointType.Production:
                if (meteringPointMasterData.EnergySupplier is not null)
                {
                    receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                }

                receivers.Add(DanishEnergyAgencyReceiver());

                break;
            case var _ when meteringPointType == MeteringPointType.Exchange:
                receivers.AddRange(
                    meteringPointMasterData.CurrentNeighborGridAreaOwners
                        .Select(ActorNumber.Create)
                        .Select(GridAccessProviderReceiver));
                break;
            case var _ when meteringPointType == MeteringPointType.VeProduction:
                receivers.Add(SystemOperatorReceiver());
                receivers.Add(DanishEnergyAgencyReceiver());

                if (meteringPointMasterData.EnergySupplier is not null)
                {
                    receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                }

                break;

            // Electrical heating, net consumption and capacity settlement metering points always sends to the energy supplier and grid access provider
            case var _ when meteringPointType == MeteringPointType.ElectricalHeating:
            case var _ when meteringPointType == MeteringPointType.NetConsumption:
            case var _ when meteringPointType == MeteringPointType.CapacitySettlement:
                // If no parent is assigned, we never send to the energy supplier, even tough one is assigned.
                if (meteringPointMasterData.ParentMeteringPointId is not null)
                {
                    // There can be periods where no energy supplier is assigned to the parent/child metering point,
                    // thus we can only send to the energy supplier if there actually is one.
                    if (meteringPointMasterData.EnergySupplier is not null)
                        receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                }

                receivers.Add(GridAccessProviderReceiver(meteringPointMasterData.CurrentGridAccessProvider));
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
            case var _ when meteringPointType == MeteringPointType.ElectricalHeating:
            case var _ when meteringPointType == MeteringPointType.NetConsumption:
            case var _ when meteringPointType == MeteringPointType.OtherConsumption:
            case var _ when meteringPointType == MeteringPointType.OtherProduction:
            case var _ when meteringPointType == MeteringPointType.CapacitySettlement:
            case var _ when meteringPointType == MeteringPointType.ExchangeReactiveEnergy:
            case var _ when meteringPointType == MeteringPointType.CollectiveNetProduction:
            case var _ when meteringPointType == MeteringPointType.CollectiveNetConsumption:
            case var _ when meteringPointType == MeteringPointType.InternalUse:
                // If no parent is assigned, we never send to the energy supplier, even tough one is assigned.
                if (meteringPointMasterData.ParentMeteringPointId is not null)
                {
                    // It is legal for the energy supplier to be null for these metering point types
                    // It is however not legal for the parent metering point to be null
                    if (meteringPointMasterData.EnergySupplier is not null)
                    {
                        receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(meteringPointType), meteringPointType.Name, $"Invalid metering point type (MeteringPointId={meteringPointMasterData.MeteringPointId.Value}).");
        }

        var distinctReceivers = receivers
            .DistinctBy(r => (r.Number.Value, r.Role.Name))
            .ToList();

        return distinctReceivers;
    }

    private Actor EnergySupplierReceiver(ActorNumber energySupplierId) =>
        new(energySupplierId, ActorRole.EnergySupplier);

    private Actor GridAccessProviderReceiver(ActorNumber gridAccessProviderId) => new(
        gridAccessProviderId,
        ActorRole.GridAccessProvider);

    private Actor DanishEnergyAgencyReceiver() => new(
        ActorNumber.Create(DataHubDetails.DanishEnergyAgencyNumber),
        ActorRole.DanishEnergyAgency);

    private Actor SystemOperatorReceiver() => new(
        ActorNumber.Create(DataHubDetails.SystemOperatorNumber),
        ActorRole.SystemOperator);

    public sealed record FindReceiversInput(
        string MeteringPointId,
        Instant StartDateTime,
        Instant EndDateTime,
        Resolution Resolution,
        IReadOnlyCollection<MeteringPointMasterData> MasterData,
        IReadOnlyCollection<ReceiversWithMeasureData.MeasureData> MeasureData);

    private sealed record MasterDataWithMeteredData
    {
        public MasterDataWithMeteredData(
            MeteringPointMasterData masterData,
            Instant inputPeriodStart,
            Instant inputPeriodEnd,
            List<ReceiversWithMeasureData.MeasureData> measureData)
        {
            MasterData = masterData;
            ValidFrom = Instant.Max(inputPeriodStart, masterData.ValidFrom.ToInstant());
            ValidTo = Instant.Min(inputPeriodEnd, masterData.ValidTo.ToInstant());
            MeasureData = measureData;
        }

        public MeteringPointMasterData MasterData { get; }

        public Instant ValidFrom { get; }

        public Instant ValidTo { get; }

        public List<ReceiversWithMeasureData.MeasureData> MeasureData { get; }
    }
}
