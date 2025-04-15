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

    internal async Task<IReadOnlyCollection<MeteringPointMasterData>> GetMasterData(
        string meteringPointId,
        string startDate,
        string endDate)
    {
        var id = new ElectricityMarketModels.MeteringPointIdentification(meteringPointId);
        var startDateTime = InstantPatternWithOptionalSeconds.Parse(startDate);
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(endDate);

        if (!startDateTime.Success || !endDateTime.Success)
        {
            return [];
        }

        IEnumerable<ElectricityMarketModels.MeteringPointMasterData> masterDataChanges;
        try
        {
            masterDataChanges = await _electricityMarketViews
                .GetMeteringPointMasterDataChangesAsync(id, new Interval(startDateTime.Value, endDateTime.Value))
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                $"Failed to get metering point master data for metering point '{meteringPointId}' in the period {startDateTime.Value}--{endDateTime.Value}.");

            return [];
        }

        var meteringPointMasterData = masterDataChanges.OrderBy(mpmd => mpmd.ValidFrom).ToList();
        if (meteringPointMasterData.Count <= 0)
        {
            return [];
        }

        var parents = meteringPointMasterData
            .Select(mpmd => (Id: mpmd.ParentIdentification, From: mpmd.ValidFrom, To: mpmd.ValidTo))
            .Where(parent => parent.Id is not null)
            .ToList();

        var parentMeteringPointMasterData = new Dictionary<string, IReadOnlyCollection<ElectricityMarketModels.MeteringPointMasterData>>();
        foreach (var (parentId, parentFrom, parentTo) in parents)
        {
            try
            {
                var newParentMasterData = (await _electricityMarketViews
                    .GetMeteringPointMasterDataChangesAsync(
                        new ElectricityMarketModels.MeteringPointIdentification(parentId!.Value),
                        new Interval(parentFrom, parentTo))
                    .ConfigureAwait(false)).ToImmutableList();

                if (parentMeteringPointMasterData.TryGetValue(parentId.Value, out var existingParentMasterData))
                {
                    parentMeteringPointMasterData[parentId.Value] = [.. existingParentMasterData, .. newParentMasterData];
                }
                else
                {
                    parentMeteringPointMasterData.Add(parentId.Value, newParentMasterData);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    $"Failed to get metering point master data for parent metering point '{parentId}' in the period {parentFrom}--{parentTo}.");
            }
        }

        if (parentMeteringPointMasterData.Count != parents.Count)
        {
            _logger.LogError(
                $"Parent metering point master data count '{parentMeteringPointMasterData.Count}' does not match the number of parent metering points '{parents.Count}'.");

            return [];
        }

        // Try-catch to prevent PREPROD from going up in flames
        IReadOnlyCollection<MeteringPointMasterData> meteringPointMasterDataPoints;
        try
        {
            meteringPointMasterDataPoints = meteringPointMasterData
                .SelectMany(mpmd => GetMeteringPointMasterDataPerEnergySupplier(mpmd, parentMeteringPointMasterData))
                .ToList()
                .AsReadOnly();
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                $"Failed to unpack metering point master data for '{meteringPointId}' in the period {startDateTime.Value}--{endDateTime.Value}.");

            return [];
        }

        return meteringPointMasterDataPoints;
    }

    private IReadOnlyCollection<MeteringPointMasterData> GetMeteringPointMasterDataPerEnergySupplier(
        ElectricityMarketModels.MeteringPointMasterData meteringPointMasterData,
        IReadOnlyDictionary<string, IReadOnlyCollection<ElectricityMarketModels.MeteringPointMasterData>> parentMeteringPointMasterData) =>
            meteringPointMasterData.ParentIdentification is not null
                ? FlattenMasterDataForChild(meteringPointMasterData, parentMeteringPointMasterData)
                : FlattenMasterDataForParent(meteringPointMasterData);

    private IReadOnlyCollection<MeteringPointMasterData> FlattenMasterDataForParent(
        ElectricityMarketModels.MeteringPointMasterData meteringPointMasterData)
    {
        if (meteringPointMasterData.EnergySuppliers.Count <= 0)
        {
            return
            [
                CreateMeteringPointMasterData(
                    meteringPointMasterData,
                    meteringPointMasterData.ValidFrom.ToDateTimeOffset(),
                    meteringPointMasterData.ValidTo.ToDateTimeOffset()),
            ];
        }

        return meteringPointMasterData.EnergySuppliers
            .Select(
                meteringPointEnergySupplier =>
                    CreateMeteringPointMasterData(
                        meteringPointMasterData,
                        meteringPointEnergySupplier.StartDate.ToDateTimeOffset(),
                        meteringPointEnergySupplier.EndDate.ToDateTimeOffset(),
                        null,
                        ActorNumber.Create(meteringPointEnergySupplier.EnergySupplier)))
            .ToList()
            .AsReadOnly();
    }

    private IReadOnlyCollection<MeteringPointMasterData> FlattenMasterDataForChild(
        Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData.MeteringPointMasterData meteringPointMasterData,
        IReadOnlyDictionary<string, IReadOnlyCollection<Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData.MeteringPointMasterData>> parentMeteringPointMasterData) =>
        parentMeteringPointMasterData[meteringPointMasterData.ParentIdentification!.Value]
                .SelectMany(
                    mpmd =>
                    {
                        if (mpmd.EnergySuppliers.Count <= 0)
                        {
                            return
                            [
                                CreateMeteringPointMasterData(
                                    meteringPointMasterData,
                                    mpmd.ValidFrom.ToDateTimeOffset(),
                                    mpmd.ValidTo.ToDateTimeOffset(),
                                    new MeteringPointId(mpmd.Identification.Value)),
                            ];
                        }

                        return mpmd.EnergySuppliers
                            .Select(
                                mpes => CreateMeteringPointMasterData(
                                    meteringPointMasterData,
                                    mpes.StartDate.ToDateTimeOffset(),
                                    mpes.EndDate.ToDateTimeOffset(),
                                    new MeteringPointId(mpmd.Identification.Value),
                                    ActorNumber.Create(mpes.EnergySupplier)));
                    })
                .ToList()
                .AsReadOnly();

    private MeteringPointMasterData CreateMeteringPointMasterData(
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
                ConnectionState: ElectricityMarketMasterDataMapper.ConnectionStateMap.Map(meteringPointMasterData.ConnectionState),
                MeteringPointType: ElectricityMarketMasterDataMapper.MeteringPointTypeMap.Map(meteringPointMasterData.Type),
                MeteringPointSubType: ElectricityMarketMasterDataMapper.MeteringPointSubTypeMap.Map(meteringPointMasterData.SubType),
                Resolution: ElectricityMarketMasterDataMapper.ResolutionMap.Map(meteringPointMasterData.Resolution.Value),
                MeasurementUnit: ElectricityMarketMasterDataMapper.MeasureUnitMap.Map(meteringPointMasterData.Unit),
                ProductId: meteringPointMasterData.ProductId.ToString(),
                ParentMeteringPointId: parentId,
                EnergySupplier: energySupplier);
}
