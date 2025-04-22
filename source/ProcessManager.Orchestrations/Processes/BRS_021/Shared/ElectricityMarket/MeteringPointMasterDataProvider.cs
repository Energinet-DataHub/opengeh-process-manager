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
using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using Microsoft.Extensions.Logging;
using NodaTime;

using ElectricityMarketModels = Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;

[SuppressMessage(
    "StyleCop.CSharp.ReadabilityRules",
    "SA1118:Parameter should not span multiple lines",
    Justification = "Readability")]
public class MeteringPointMasterDataProvider(
    IElectricityMarketViews electricityMarketViews,
    ILogger<MeteringPointMasterDataProvider> logger)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;
    private readonly ILogger<MeteringPointMasterDataProvider> _logger = logger;

    internal Task<IReadOnlyCollection<MeteringPointMasterData>> GetMasterData(
        string meteringPointId,
        string startDate,
        string endDate)
    {
        var startDateTime = InstantPatternWithOptionalSeconds.Parse(startDate);
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(endDate);

        if (!startDateTime.Success || !endDateTime.Success)
            return Task.FromResult<IReadOnlyCollection<MeteringPointMasterData>>([]);

        return GetMasterData(meteringPointId, startDateTime.Value, endDateTime.Value);
    }

    internal async Task<IReadOnlyCollection<MeteringPointMasterData>> GetMasterData(
        string meteringPointId,
        Instant startDateTime,
        Instant endDateTime)
    {
        var id = new ElectricityMarketModels.MeteringPointIdentification(meteringPointId);

        IEnumerable<ElectricityMarketModels.MeteringPointMasterData> masterDataChanges;

        try
        {
            masterDataChanges = await _electricityMarketViews
                .GetMeteringPointMasterDataChangesAsync(id, new Interval(startDateTime, endDateTime))
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                $"Failed to get metering point master data for '{meteringPointId}' in {startDateTime}–{endDateTime}.");
            return [];
        }

        var meteringPointMasterDataList = masterDataChanges.OrderBy(mpmd => mpmd.ValidFrom).ToList();

        // Collect parent periods needed
        var parentsToFetch = meteringPointMasterDataList
            .Where(mpmd => mpmd.ParentIdentification is not null)
            .Select(
                mpmd => (
                    Id: mpmd.ParentIdentification!.Value,
                    From: mpmd.ValidFrom,
                    To: mpmd.ValidTo))
            .ToList();

        var parentData = new Dictionary<string, List<ElectricityMarketModels.MeteringPointMasterData>>();

        foreach (var (parentId, from, to) in parentsToFetch)
        {
            try
            {
                var parentMasterData = await _electricityMarketViews
                    .GetMeteringPointMasterDataChangesAsync(
                        new ElectricityMarketModels.MeteringPointIdentification(parentId),
                        new Interval(from, to))
                    .ConfigureAwait(false);

                if (!parentData.ContainsKey(parentId))
                    parentData[parentId] = [];

                parentData[parentId].AddRange(parentMasterData);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to get master data for parent '{parentId}' during {from}–{to}.");
            }
        }

        var result = new List<MeteringPointMasterData>();

        foreach (var child in meteringPointMasterDataList)
        {
            var from = child.ValidFrom;
            var to = child.ValidTo;

            // If no parent: use the child's own energy supplier (even if null)
            if (child.ParentIdentification is null)
            {
                result.Add(
                    CreatePmMeteringPointMasterData(
                        child,
                        from.ToDateTimeOffset(),
                        to.ToDateTimeOffset(),
                        null,
                        child.EnergySupplier is not null ? ActorNumber.Create(child.EnergySupplier) : null));
                continue;
            }

            var parentId = child.ParentIdentification.Value;

            // If no parent data was fetched, fall back to child supplier
            if (!parentData.TryGetValue(parentId, out var parentEntries) || parentEntries.Count == 0)
            {
                result.Add(
                    CreatePmMeteringPointMasterData(
                        child,
                        from.ToDateTimeOffset(),
                        to.ToDateTimeOffset(),
                        new MeteringPointId(parentId),
                        child.EnergySupplier is not null ? ActorNumber.Create(child.EnergySupplier) : null));
                continue;
            }

            // STEP 1: Identify time boundaries where supplier might change
            var sliceBoundaries = new SortedSet<Instant> { from, to };

            foreach (var parent in parentEntries)
            {
                var overlapsChild = parent.ValidFrom < to && parent.ValidTo > from;
                if (!overlapsChild) continue;

                var overlapStart = Instant.Max(from, parent.ValidFrom);
                var overlapEnd = Instant.Min(to, parent.ValidTo);

                sliceBoundaries.Add(overlapStart);
                sliceBoundaries.Add(overlapEnd);
            }

            // STEP 2: Build one segment per interval between boundaries
            var orderedBoundaries = sliceBoundaries.OrderBy(b => b).ToList();

            for (var i = 0; i < orderedBoundaries.Count - 1; i++)
            {
                var sliceFrom = orderedBoundaries[i];
                var sliceTo = orderedBoundaries[i + 1];

                // Find parent supplier valid in this slice
                var matchingParent = parentEntries.FirstOrDefault(
                    p =>
                        p.ValidFrom <= sliceFrom &&
                        p.ValidTo >= sliceTo &&
                        p.EnergySupplier is not null);

                var supplier = matchingParent?.EnergySupplier ?? child.EnergySupplier;

                result.Add(
                    CreatePmMeteringPointMasterData(
                        child,
                        sliceFrom.ToDateTimeOffset(),
                        sliceTo.ToDateTimeOffset(),
                        new MeteringPointId(parentId),
                        supplier is not null ? ActorNumber.Create(supplier) : null));
            }
        }

        return result.AsReadOnly();
    }

    private static MeteringPointMasterData CreatePmMeteringPointMasterData(
        ElectricityMarketModels.MeteringPointMasterData meteringPointMasterData,
        DateTimeOffset validFrom,
        DateTimeOffset validTo,
        MeteringPointId? parentId = null,
        ActorNumber? energySupplier = null) =>
        new(
            MeteringPointId: new MeteringPointId(meteringPointMasterData.Identification.Value),
            ValidFrom: validFrom,
            ValidTo: validTo,
            GridAreaCode: new GridAreaCode(meteringPointMasterData.GridAreaCode.Value),
            GridAccessProvider: ActorNumber.Create(meteringPointMasterData.GridAccessProvider),
            NeighborGridAreaOwners: meteringPointMasterData.NeighborGridAreaOwners,
            ConnectionState: ElectricityMarketMasterDataMapper.ConnectionStateMap.Map(
                meteringPointMasterData.ConnectionState),
            MeteringPointType: ElectricityMarketMasterDataMapper.MeteringPointTypeMap.Map(meteringPointMasterData.Type),
            MeteringPointSubType: ElectricityMarketMasterDataMapper.MeteringPointSubTypeMap.Map(
                meteringPointMasterData.SubType),
            Resolution: ElectricityMarketMasterDataMapper.ResolutionMap.Map(meteringPointMasterData.Resolution.Value),
            MeasurementUnit: ElectricityMarketMasterDataMapper.MeasureUnitMap.Map(meteringPointMasterData.Unit),
            ProductId: meteringPointMasterData.ProductId.ToString(),
            ParentMeteringPointId: parentId,
            EnergySupplier: energySupplier);
}
