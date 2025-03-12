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
using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Mapper;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Mapper;
using NodaTime;
using PMGridAreaCode =
    Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.GridAreaCode;
using PMMeteringPointMasterData =
    Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.
    MeteringPointMasterData;
using PMResolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Thingies;

[SuppressMessage(
    "StyleCop.CSharp.ReadabilityRules",
    "SA1118:Parameter should not span multiple lines",
    Justification = "Readability")]
public class MeteringPointMasterDataSteamRoller(IElectricityMarketViews electricityMarketViews)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;

    internal async Task<IReadOnlyCollection<PMMeteringPointMasterData>> GetAndSteamRollMasterData(
        string meteringPointId,
        string startDate,
        string endDate)
    {
        var id = new MeteringPointIdentification(meteringPointId);
        var startDateTime = InstantPatternWithOptionalSeconds.Parse(startDate);
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(endDate);

        var meteringPointMasterData =
            (await _electricityMarketViews
                .GetMeteringPointMasterDataChangesAsync(id, new Interval(startDateTime.Value, endDateTime.Value))
                .ConfigureAwait(false))
            .SelectMany(SteamRoll)
            .ToList()
            .AsReadOnly();

        return meteringPointMasterData;
    }

    private IReadOnlyCollection<PMMeteringPointMasterData> SteamRoll(MeteringPointMasterData meteringPointMasterData)
    {
        return meteringPointMasterData.EnergySuppliers
            .Select(
                meteringPointEnergySupplier => new PMMeteringPointMasterData(
                    new MeteringPointId(meteringPointMasterData.Identification.Value),
                    meteringPointEnergySupplier.StartDate.ToDateTimeOffset(),
                    meteringPointEnergySupplier.EndDate.ToDateTimeOffset(),
                    new PMGridAreaCode(meteringPointMasterData.GridAreaCode.Value),
                    ActorNumber.Create(meteringPointMasterData.GridAccessProvider),
                    meteringPointMasterData.NeighborGridAreaOwners,
                    MeteringPointMasterDataMapper.ConnectionStateMap.Map(meteringPointMasterData.ConnectionState),
                    MeteringPointMasterDataMapper.MeteringPointTypeMap.Map(meteringPointMasterData.Type),
                    MeteringPointMasterDataMapper.MeteringPointSubTypeMap.Map(meteringPointMasterData.SubType),
                    PMResolution.FromName(meteringPointMasterData.Resolution.Value),
                    MeteringPointMasterDataMapper.MeasureUnitMap.Map(meteringPointMasterData.Unit),
                    meteringPointMasterData.ProductId.ToString(),
                    meteringPointMasterData.ParentIdentification is null
                        ? null
                        : new MeteringPointId(meteringPointMasterData.ParentIdentification.Value),
                    ActorNumber.Create(meteringPointEnergySupplier.EnergySupplier)))
            .ToList()
            .AsReadOnly();
    }
}
