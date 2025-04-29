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
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
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
    ILogger<MeteringPointMasterDataProvider> logger,
    IClock clock,
    IFeatureFlagManager featureFlagManager)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;
    private readonly ILogger<MeteringPointMasterDataProvider> _logger = logger;
    private readonly IClock _clock = clock;
    private readonly IFeatureFlagManager _featureFlagManager = featureFlagManager;

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
        ElectricityMarketModels.MeteringPointMasterData currentMasterDataChanges;

        try
        {
            masterDataChanges = await _electricityMarketViews
                .GetMeteringPointMasterDataChangesAsync(id, new Interval(startDateTime, endDateTime))
                .ConfigureAwait(false);

            // Get current master data to get the current grid area owner, and current grid areas neighboring grid area owners.
            currentMasterDataChanges = (await _electricityMarketViews
                .GetMeteringPointMasterDataChangesAsync(id, new Interval(_clock.GetCurrentInstant(), _clock.GetCurrentInstant().PlusSeconds(1)))
                .ConfigureAwait(false)).Single();

            if (await IsPerformanceTest(meteringPointId).ConfigureAwait(false))
            {
                masterDataChanges = GetMasterDataForPerformanceTest(
                    id,
                    startDateTime,
                    endDateTime);

                currentMasterDataChanges = GetMasterDataForPerformanceTest(
                    id,
                    _clock.GetCurrentInstant(),
                    _clock.GetCurrentInstant().PlusSeconds(1)).Single();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                $"Failed to get metering point master data for '{meteringPointId}' in {startDateTime}–{endDateTime}.");
            return [];
        }

        var meteringPointMasterDataList = masterDataChanges.OrderBy(mpmd => mpmd.ValidFrom).ToList();

        var result = new List<MeteringPointMasterData>();

        foreach (var meteringPointMasterData in meteringPointMasterDataList)
        {
            var from = meteringPointMasterData.ValidFrom;
            var to = meteringPointMasterData.ValidTo;

            // If no parent: use own energy supplier (even if null)
            if (meteringPointMasterData.ParentIdentification is null)
            {
                result.Add(
                    CreateMeteringPointMasterData(
                        meteringPointMasterData,
                        currentMasterDataChanges,
                        from.ToDateTimeOffset(),
                        to.ToDateTimeOffset(),
                        null,
                        meteringPointMasterData.EnergySupplier is not null
                            ? ActorNumber.Create(meteringPointMasterData.EnergySupplier)
                            : null));
                continue;
            }

            var parentId = meteringPointMasterData.ParentIdentification.Value;
            var parentMasterData = (await _electricityMarketViews
                    .GetMeteringPointMasterDataChangesAsync(
                        new ElectricityMarketModels.MeteringPointIdentification(parentId),
                        new Interval(from, to))
                    .ConfigureAwait(false))
                .ToList();

            // If no parent data was fetched, fall back to own energy supplier (even if null)
            if (parentMasterData.Count == 0)
            {
                // TODO: Should this throw an exception instead?
                result.Add(
                    CreateMeteringPointMasterData(
                        meteringPointMasterData,
                        currentMasterDataChanges,
                        from.ToDateTimeOffset(),
                        to.ToDateTimeOffset(),
                        new MeteringPointId(parentId),
                        meteringPointMasterData.EnergySupplier is not null
                            ? ActorNumber.Create(meteringPointMasterData.EnergySupplier)
                            : null));
                continue;
            }

            // STEP 1: Identify time boundaries where energy supplier might change
            var parentMasterDataPeriods = new List<Interval>();

            foreach (var parent in parentMasterData.OrderBy(p => p.ValidFrom).ThenBy(p => p.ValidTo))
            {
                var overlapStart = Instant.Max(from, parent.ValidFrom);
                var overlapEnd = Instant.Min(to, parent.ValidTo);

                var period = new Interval(overlapStart, overlapEnd);

                parentMasterDataPeriods.Add(period);
            }

            // STEP 2: Build one segment per interval between boundaries
            foreach (var currentPeriod in parentMasterDataPeriods)
            {
                // Find parent supplier valid in this slice
                var matchingParent = parentMasterData.Single(
                    p =>
                        p.ValidFrom <= currentPeriod.Start &&
                        p.ValidTo >= currentPeriod.End);

                var supplier = matchingParent.EnergySupplier;

                result.Add(
                    CreateMeteringPointMasterData(
                        meteringPointMasterData,
                        currentMasterDataChanges,
                        currentPeriod.Start.ToDateTimeOffset(),
                        currentPeriod.End.ToDateTimeOffset(),
                        new MeteringPointId(parentId),
                        supplier is not null ? ActorNumber.Create(supplier) : null));
            }
        }

        return result.AsReadOnly();
    }

    private static MeteringPointMasterData CreateMeteringPointMasterData(
        ElectricityMarketModels.MeteringPointMasterData meteringPointMasterData,
        ElectricityMarketModels.MeteringPointMasterData currentMeteringPointMasterData,
        DateTimeOffset validFrom,
        DateTimeOffset validTo,
        MeteringPointId? parentId = null,
        ActorNumber? energySupplier = null) => new(
        MeteringPointId: new MeteringPointId(meteringPointMasterData.Identification.Value),
        ValidFrom: validFrom,
        ValidTo: validTo,
        CurrentGridAreaCode: new GridAreaCode(currentMeteringPointMasterData.GridAreaCode.Value),
        CurrentGridAccessProvider: ActorNumber.Create(currentMeteringPointMasterData.GridAccessProvider),
        CurrentNeighborGridAreaOwners: currentMeteringPointMasterData.NeighborGridAreaOwners,
        ConnectionState: ElectricityMarketMasterDataMapper.ConnectionStateMap.Map(meteringPointMasterData.ConnectionState),
        MeteringPointType: ElectricityMarketMasterDataMapper.MeteringPointTypeMap.Map(meteringPointMasterData.Type),
        MeteringPointSubType: ElectricityMarketMasterDataMapper.MeteringPointSubTypeMap.Map(meteringPointMasterData.SubType),
        Resolution: ElectricityMarketMasterDataMapper.ResolutionMap.Map(meteringPointMasterData.Resolution.Value),
        MeasurementUnit: ElectricityMarketMasterDataMapper.MeasureUnitMap.Map(meteringPointMasterData.Unit),
        ProductId: meteringPointMasterData.ProductId.ToString(),
        ParentMeteringPointId: parentId,
        EnergySupplier: energySupplier);

    private static IEnumerable<ElectricityMarketModels.MeteringPointMasterData> GetMasterDataForPerformanceTest(
        ElectricityMarketModels.MeteringPointIdentification id,
        Instant startDateTime,
        Instant endDateTime)
    {
        return new List<ElectricityMarketModels.MeteringPointMasterData>()
        {
            new()
            {
                Identification = id,
                ValidFrom = startDateTime,
                ValidTo = endDateTime,
                GridAreaCode = new ElectricityMarketModels.GridAreaCode("000"),
                GridAccessProvider = "1111111111100", // should be aligned with incoming message for performance test
                ConnectionState = ElectricityMarketModels.ConnectionState.Connected,
                Type = ElectricityMarketModels.MeteringPointType.Production,
                SubType = ElectricityMarketModels.MeteringPointSubType.Physical,
                Resolution = new Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData.Resolution("PT1H"),
                Unit = ElectricityMarketModels.MeasureUnit.kWh,
                ProductId = ElectricityMarketModels.ProductId.Tariff,
                ParentIdentification = null,
                EnergySupplier = "1111111111111", // should be aligned with performance test
            },
        };
    }

    private async Task<bool> IsPerformanceTest(string meteringPointId)
    {
        var performanceTestEnabled = await _featureFlagManager
            .IsEnabledAsync(FeatureFlag.EnableBrs021ForwardMeteredDataPerformanceTest)
            .ConfigureAwait(false);
        var isInputTestData = meteringPointId.IsTestUuid();

        return performanceTestEnabled && isInputTestData;
    }
}
