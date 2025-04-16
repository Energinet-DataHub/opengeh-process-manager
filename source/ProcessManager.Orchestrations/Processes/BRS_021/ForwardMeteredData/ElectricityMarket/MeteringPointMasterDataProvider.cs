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

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Mapper;
using Microsoft.Extensions.Logging;
using NodaTime;
using PMGridAreaCode =
    Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.GridAreaCode;
using PMMeteringPointMasterData =
    Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.
    MeteringPointMasterData;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;

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

    internal async Task<IReadOnlyCollection<PMMeteringPointMasterData>> GetMasterData(
    string meteringPointId,
    string startDate,
    string endDate)
{
    var id = new MeteringPointIdentification(meteringPointId);
    var startDateTime = InstantPatternWithOptionalSeconds.Parse(startDate);
    var endDateTime = InstantPatternWithOptionalSeconds.Parse(endDate);

    if (!startDateTime.Success || !endDateTime.Success)
        return [];

    IEnumerable<MeteringPointMasterData> masterDataChanges;

    try
    {
        masterDataChanges = await _electricityMarketViews
            .GetMeteringPointMasterDataChangesAsync(id, new Interval(startDateTime.Value, endDateTime.Value))
            .ConfigureAwait(false);
    }
    catch (Exception e)
    {
        _logger.LogError(e, $"Failed to get metering point master data for '{meteringPointId}' in {startDateTime.Value}–{endDateTime.Value}.");
        return [];
    }

    var meteringPointMasterDataList = masterDataChanges.OrderBy(mpmd => mpmd.ValidFrom).ToList();

    // Collect parent periods needed
    var parentsToFetch = meteringPointMasterDataList
        .Where(mpmd => mpmd.ParentIdentification is not null)
        .Select(mpmd => (
            Id: mpmd.ParentIdentification!.Value,
            From: mpmd.ValidFrom,
            To: mpmd.ValidTo))
        .ToList();

    var parentData = new Dictionary<string, List<MeteringPointMasterData>>();

    foreach (var (parentId, from, to) in parentsToFetch)
    {
        try
        {
            var parentMasterData = await _electricityMarketViews
                .GetMeteringPointMasterDataChangesAsync(new MeteringPointIdentification(parentId), new Interval(from, to))
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

    var result = new List<PMMeteringPointMasterData>();

    foreach (var child in meteringPointMasterDataList)
    {
        var from = child.ValidFrom;
        var to = child.ValidTo;

        if (child.ParentIdentification is null)
        {
            result.Add(CreatePMMeteringPointMasterData(
                child,
                from.ToDateTimeOffset(),
                to.ToDateTimeOffset(),
                null,
                child.EnergySupplier is not null ? ActorNumber.Create(child.EnergySupplier) : null));
            continue;
        }

        var parentId = child.ParentIdentification.Value;

        if (!parentData.TryGetValue(parentId, out var parentEntries) || parentEntries.Count == 0)
        {
            result.Add(CreatePMMeteringPointMasterData(
                child,
                from.ToDateTimeOffset(),
                to.ToDateTimeOffset(),
                new MeteringPointId(parentId),
                child.EnergySupplier is not null ? ActorNumber.Create(child.EnergySupplier) : null));
            continue;
        }

        // Get all boundaries for slicing: child start, end + relevant parent transitions
        var boundaries = new SortedSet<Instant> { from, to };

        foreach (var parent in parentEntries)
        {
            if (parent.ValidFrom < to && parent.ValidTo > from)
            {
                boundaries.Add(Instant.Max(from, parent.ValidFrom));
                boundaries.Add(Instant.Min(to, parent.ValidTo));
            }
        }

        var ordered = boundaries.OrderBy(b => b).ToList();

        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var sliceFrom = ordered[i];
            var sliceTo = ordered[i + 1];

            var parentSlice = parentEntries
                .FirstOrDefault(p =>
                    p.ValidFrom <= sliceFrom && p.ValidTo >= sliceTo && p.EnergySupplier is not null);

            var supplier = parentSlice?.EnergySupplier ?? child.EnergySupplier;

            result.Add(CreatePMMeteringPointMasterData(
                child,
                sliceFrom.ToDateTimeOffset(),
                sliceTo.ToDateTimeOffset(),
                new MeteringPointId(parentId),
                supplier is not null ? ActorNumber.Create(supplier) : null));
        }
    }

    return result.AsReadOnly();
}

    private PMMeteringPointMasterData CreatePMMeteringPointMasterData(
        MeteringPointMasterData meteringPointMasterData,
        DateTimeOffset validFrom,
        DateTimeOffset validTo,
        MeteringPointId? parentId = null,
        ActorNumber? energySupplier = null) => new(
        new MeteringPointId(meteringPointMasterData.Identification.Value),
        validFrom,
        validTo,
        new PMGridAreaCode(meteringPointMasterData.GridAreaCode.Value),
        ActorNumber.Create(meteringPointMasterData.GridAccessProvider),
        meteringPointMasterData.NeighborGridAreaOwners,
        MeteringPointMasterDataMapper.ConnectionStateMap.Map(meteringPointMasterData.ConnectionState),
        MeteringPointMasterDataMapper.MeteringPointTypeMap.Map(meteringPointMasterData.Type),
        MeteringPointMasterDataMapper.MeteringPointSubTypeMap.Map(meteringPointMasterData.SubType),
        MeteringPointMasterDataMapper.ResolutionMap.Map(meteringPointMasterData.Resolution.Value),
        MeteringPointMasterDataMapper.MeasureUnitMap.Map(meteringPointMasterData.Unit),
        meteringPointMasterData.ProductId.ToString(),
        parentId,
        energySupplier);
}
