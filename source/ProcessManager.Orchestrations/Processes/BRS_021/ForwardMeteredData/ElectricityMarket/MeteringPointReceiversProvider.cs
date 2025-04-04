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
using System.Globalization;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using NodaTime;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;

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

    public List<ReceiversWithMeteredDataV1> GetReceiversWithMeteredDataFromMasterDataList(
        IReadOnlyCollection<MeteringPointMasterData> meteringPointMasterDataList,
        ForwardMeteredDataInputV1 input)
    {
        if (meteringPointMasterDataList.Count == 0)
            throw new InvalidOperationException($"The metering point master data list is empty (MeteringPointId={input.MeteringPointId}, StartDateTime={input.StartDateTime}, EndDateTime={input.EndDateTime})");

        // Ensure metered data is sorted by position
        var sortedMeteredData = new SortedDictionary<int, ReceiversWithMeteredDataV1.AcceptedMeteredData>(
            input.MeteredDataList.ToDictionary(
                md => int.Parse(md.Position!),
                md =>
                {
                    // TODO: Shouldn't position be an int? in the input?
                    var position = int.Parse(md.Position!);

                    // TODO: Shouldn't EnergyQuantity be a decimal? in the input?
                    var canParseEnergyQuantity = decimal.TryParse(
                        md.EnergyQuantity,
                        CultureInfo.InvariantCulture,
                        out var energyQuantity);

                    // The input is already validated, so converting these should not fail.
                    return new ReceiversWithMeteredDataV1.AcceptedMeteredData(
                        Position: position,
                        EnergyQuantity: canParseEnergyQuantity ? energyQuantity : null,
                        QuantityQuality: Quality.FromNameOrDefault(md.QuantityQuality));
                }));

        // Ensure master data is sorted by ValidFrom
        var masterDataDictionary = meteringPointMasterDataList.ToDictionary(mpmd => mpmd.ValidFrom.ToInstant());

        // The input is already validated, so this parsing should never fail
        var totalPeriodStart = InstantPatternWithOptionalSeconds.Parse(input.StartDateTime).Value;
        var totalPeriodEnd = InstantPatternWithOptionalSeconds.Parse(input.EndDateTime!).Value;

        var allReceivers = CalculateReceiversWithMeteredDataForMasterDataPeriods(
            totalPeriodStart,
            totalPeriodEnd,
            Resolution.FromName(input.Resolution!), // Resolution shouldn't change between master data periods, else validation should fail
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
    private List<ReceiversWithMeteredDataV1> CalculateReceiversWithMeteredDataForMasterDataPeriods(
        Instant totalPeriodStart,
        Instant totalPeriodEnd,
        Resolution resolution,
        Dictionary<Instant, MeteringPointMasterData> masterData,
        SortedDictionary<int, ReceiversWithMeteredDataV1.AcceptedMeteredData> sortedMeteredData)
    {
        if (masterData.Count == 1)
        {
            // If there is only one master data element then we can skip a lot of logic, since we don't
            // need to split the metered data into periods and recalculate positions
            var masterDataElement = masterData.Values.Single();

            // Ensure that the start and end dates are bounded by the period
            masterDataElement = masterDataElement with
            {
                ValidFrom = masterDataElement.ValidFrom > totalPeriodStart.ToDateTimeOffset()
                    ? masterDataElement.ValidFrom
                    : totalPeriodStart.ToDateTimeOffset(),
                ValidTo = masterDataElement.ValidTo > totalPeriodEnd.ToDateTimeOffset()
                    ? totalPeriodEnd.ToDateTimeOffset()
                    : masterDataElement.ValidTo,
            };

            var receiversWithMeteredData = CreateReceiversWithMeteredData(
                new MasterDataWithMeteredData(
                    masterDataElement,
                    totalPeriodStart,
                    totalPeriodEnd,
                    [.. sortedMeteredData.Values]));

            return [receiversWithMeteredData];
        }

        var currentTimestamp = totalPeriodStart;

        var firstMasterData = masterData.Values.First();
        var currentMasterData = new MasterDataWithMeteredData(firstMasterData, totalPeriodStart, totalPeriodEnd, []);

        List<MasterDataWithMeteredData> masterDataWithMeteredDataList = [currentMasterData];

        foreach (var meteredData in sortedMeteredData.Values)
        {
            // If current timestamp is equal to (or later than) the total period, throw an exception. This assumes totalPeriodEnd is exclusive.
            if (currentTimestamp >= totalPeriodEnd)
                throw new InvalidOperationException($"The current timestamp is after the metered data period (Position={meteredData.Position}, CurrentTimestamp={currentTimestamp}, PeriodEnd={totalPeriodEnd})");

            // Get master data for current timestamp. This assumes ValidTo is exclusive.
            var currentTimestampBelongsToNextMasterDataPeriod = currentTimestamp >= currentMasterData.ValidTo;
            if (currentTimestampBelongsToNextMasterDataPeriod)
            {
                // The master data should always be continuous (with no overlaps), so if this fails then
                // the master data (or our implementation) has a bug.
                if (!masterData.TryGetValue(currentTimestamp, out var nextMasterData))
                    throw new InvalidOperationException($"The master data for the current timestamp is missing (MeteringPointId={currentMasterData.MasterData.MeteringPointId.Value}, Position={meteredData.Position}, CurrentTimestamp={currentTimestamp})");

                currentMasterData = new MasterDataWithMeteredData(nextMasterData, totalPeriodStart, totalPeriodEnd, []);

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
            var newPosition = currentMasterData.MeteredDataList.Count + 1;
            currentMasterData.MeteredDataList.Add(meteredData with { Position = newPosition });

            // Get next timestamp
            currentTimestamp = AddResolutionToTimestamp(currentTimestamp, resolution);
        }

        return masterDataWithMeteredDataList
            .Select(md => CreateReceiversWithMeteredData(md))
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

    private ReceiversWithMeteredDataV1 CreateReceiversWithMeteredData(
        MasterDataWithMeteredData masterDataWithMeteredData)
    {
        var actorReceivers = GetReceiversFromMasterData(masterDataWithMeteredData.MasterData);

        return new ReceiversWithMeteredDataV1(
            Actors: actorReceivers,
            Resolution: masterDataWithMeteredData.MasterData.Resolution,
            MeasureUnit: masterDataWithMeteredData.MasterData.MeasurementUnit,
            StartDateTime: masterDataWithMeteredData.ValidFrom.ToDateTimeOffset(),
            EndDateTime: masterDataWithMeteredData.ValidTo.ToDateTimeOffset(),
            MeteredData: masterDataWithMeteredData.MeteredDataList);
    }

    private List<MarketActorRecipientV1> GetReceiversFromMasterData(
        MeteringPointMasterData meteringPointMasterData)
    {
        var receivers = new List<MarketActorRecipientV1>();
        var meteringPointType = meteringPointMasterData.MeteringPointType;

        switch (meteringPointType)
        {
            case var _ when meteringPointType == MeteringPointType.Consumption:
            case var _ when meteringPointType == MeteringPointType.Production:
                if (meteringPointMasterData.EnergySupplier is not null)
                {
                    receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                }

                receivers.Add(DanishEnergyAgencyReceiver());
                break;
            case var _ when meteringPointType == MeteringPointType.Exchange:
                receivers.AddRange(
                    meteringPointMasterData.NeighborGridAreaOwners
                        .Select(ActorNumber.Create)
                        .Select(NeighborGridAccessProviderReceiver));
                break;
            case var _ when meteringPointType == MeteringPointType.VeProduction:
                receivers.Add(SystemOperatorReceiver());
                receivers.Add(DanishEnergyAgencyReceiver());

                if (meteringPointMasterData.EnergySupplier is not null)
                {
                    receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                }

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
                if (meteringPointMasterData.ParentMeteringPointId is not null)
                {
                    // It is legal for the energy supplier to be null for these metering point types
                    // It is however not legal for the parent metering point to be null
                    if (meteringPointMasterData.EnergySupplier is not null)
                    {
                        receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Parent metering point is missing for child metering point type (MeteringPointId={meteringPointMasterData.MeteringPointId.Value}, MeteringPointType={meteringPointMasterData.MeteringPointType.Name}).");
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(meteringPointType), meteringPointType.Name, $"Invalid metering point type (MeteringPointId={meteringPointMasterData.MeteringPointId.Value}).");
        }

        var distinctReceivers = receivers
            .DistinctBy(r => (r.ActorNumber.Value, r.ActorRole.Name))
            .ToList();

        return distinctReceivers;
    }

    private MarketActorRecipientV1 EnergySupplierReceiver(ActorNumber energySupplierId) =>
        new(energySupplierId, ActorRole.EnergySupplier);

    private MarketActorRecipientV1 NeighborGridAccessProviderReceiver(ActorNumber neighborGridAccessProviderId) => new(
        neighborGridAccessProviderId,
        ActorRole.GridAccessProvider);

    private MarketActorRecipientV1 DanishEnergyAgencyReceiver() => new(
        ActorNumber.Create(DataHubDetails.DanishEnergyAgencyNumber),
        ActorRole.DanishEnergyAgency);

    private MarketActorRecipientV1 SystemOperatorReceiver() => new(
        ActorNumber.Create(DataHubDetails.SystemOperatorNumber),
        ActorRole.SystemOperator);

    private record MasterDataWithMeteredData
    {
        public MasterDataWithMeteredData(
            MeteringPointMasterData masterData,
            Instant inputPeriodStart,
            Instant inputPeriodEnd,
            List<ReceiversWithMeteredDataV1.AcceptedMeteredData> meteredDataList)
        {
            MasterData = masterData;
            ValidFrom = Instant.Max(inputPeriodStart, masterData.ValidFrom.ToInstant());
            ValidTo = Instant.Min(inputPeriodEnd, masterData.ValidTo.ToInstant());
            MeteredDataList = meteredDataList;
        }

        public MeteringPointMasterData MasterData { get; }

        public Instant ValidFrom { get; }

        public Instant ValidTo { get; }

        public List<ReceiversWithMeteredDataV1.AcceptedMeteredData> MeteredDataList { get; }

        public void Deconstruct(
            out MeteringPointMasterData masterData,
            out Instant validFrom,
            out Instant validTo,
            out List<ReceiversWithMeteredDataV1.AcceptedMeteredData> meteredDataList)
        {
            masterData = MasterData;
            validFrom = ValidFrom;
            validTo = ValidTo;
            meteredDataList = MeteredDataList;
        }
    }
}
