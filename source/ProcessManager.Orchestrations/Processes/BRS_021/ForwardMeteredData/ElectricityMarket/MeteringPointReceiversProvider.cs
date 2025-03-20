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

using System.Globalization;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using NodaTime;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;

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
        var masterDataDictionary = meteringPointMasterDataList
            .ToDictionary(mpmd => mpmd.ValidFrom.ToInstant());

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
        var currentTimestamp = totalPeriodStart;

        var currentMasterData = new MasterDataWithMeteredData(
            MasterData: masterData[currentTimestamp],
            ValidFrom: masterData[currentTimestamp].ValidFrom.ToInstant(),
            ValidTo: masterData[currentTimestamp].ValidTo.ToInstant(),
            MeteredDataList: []);

        List<MasterDataWithMeteredData> masterDataWithMeteredDataList = [currentMasterData];

        foreach (var meteredData in sortedMeteredData.Values)
        {
            // TODO: Is EndDateTime inclusive or exclusive? This assumes exclusive.
            if (currentTimestamp >= totalPeriodEnd)
                throw new InvalidOperationException($"The current timestamp is after the metered data period (Position={meteredData.Position}, CurrentTimestamp={currentTimestamp}, PeriodEnd={totalPeriodEnd})");

            // Get master data for current timestamp
            // TODO: Is the ValidTo inclusive or exclusive? This assumes exclusive.
            var currentTimestampBelongsToNextMasterDataPeriod = currentTimestamp >= currentMasterData.ValidTo;
            if (currentTimestampBelongsToNextMasterDataPeriod)
            {
                // The master data should always be continuous (with no overlaps), so if this fails then
                // the master data (or our implementation) has a bug.
                var nextMasterData = masterData[currentTimestamp];
                currentMasterData = new MasterDataWithMeteredData(
                    MasterData: nextMasterData,
                    ValidFrom: nextMasterData.ValidFrom.ToInstant(),
                    ValidTo: nextMasterData.ValidTo.ToInstant(),
                    MeteredDataList: []);

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
            .Select(md => CreateReceiversWithMeteredData(md.MasterData, md.MeteredDataList))
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
        MeteringPointMasterData masterData,
        IReadOnlyCollection<ReceiversWithMeteredDataV1.AcceptedMeteredData> meteredDataForMasterDataPeriod)
    {
        var actorReceivers = GetReceiversFromMasterData(masterData);

        return new ReceiversWithMeteredDataV1(
            Actors: actorReceivers,
            Resolution: masterData.Resolution,
            MeasureUnit: masterData.MeasurementUnit,
            StartDateTime: masterData.ValidFrom,
            EndDateTime: masterData.ValidTo,
            MeteredData: meteredDataForMasterDataPeriod);
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
                receivers.Add(EnergySupplierReceiver(meteringPointMasterData.EnergySupplier));
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
                    throw new InvalidOperationException($"Parent metering point is missing for child metering point type (MeteringPointId={meteringPointMasterData.MeteringPointId.Value}, MeteringPointType={meteringPointMasterData.MeteringPointType.Name}).");
                }

                break;
            default:
                throw new NotImplementedException();
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

    private record MasterDataWithMeteredData(
        MeteringPointMasterData MasterData,
        Instant ValidFrom,
        Instant ValidTo,
        List<ReceiversWithMeteredDataV1.AcceptedMeteredData> MeteredDataList);
}
