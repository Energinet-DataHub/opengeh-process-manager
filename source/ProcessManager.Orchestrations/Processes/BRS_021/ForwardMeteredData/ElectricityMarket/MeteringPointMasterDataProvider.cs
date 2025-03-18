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
using Microsoft.Extensions.Logging;
using NodaTime;
using PMGridAreaCode =
    Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.GridAreaCode;
using PMMeteringPointMasterData =
    Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.
    MeteringPointMasterData;
using PMResolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;

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
        {
            return [];
        }

        IEnumerable<MeteringPointMasterData> masterDataChanges;
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
                $"Failed to get metering point master data for metering point '{meteringPointId}' in the period {startDateTime}--{endDateTime}.");

            return [];
        }

        var meteringPointMasterData = masterDataChanges.OrderBy(mpmd => mpmd.ValidFrom).ToList();
        if (meteringPointMasterData.Count <= 0)
        {
            return [];
        }

        // Meta master data validation
        var firstMeteringPointMasterData = meteringPointMasterData.First();
        var (firstValidatedMeteringPointMasterData, firstValidationExceptions) =
            GetMeteringPointMasterDataPerEnergySupplier(firstMeteringPointMasterData);

        var (_, meteringPointMasterDataPoints, exceptions) = meteringPointMasterData
            .Skip(1)
            .Aggregate(
                (Prev: firstMeteringPointMasterData,
                    MeteringPointMasterDataPoints: firstValidatedMeteringPointMasterData,
                    Exceptions: firstMeteringPointMasterData.Identification.Value == meteringPointId
                        ? firstValidationExceptions.ToList()
                        :
                        [
                            .. firstValidationExceptions,
                            new MeteringPointMasterDataInconsistencyException(
                                $"Metering point id '{firstMeteringPointMasterData.Identification.Value}' is not equal to the requested metering point id '{meteringPointId}'"),
                        ]),
                (acc, next) =>
                {
                    var exceptions = new List<MeteringPointMasterDataInconsistencyException>();

                    if (next.ValidFrom != acc.Prev.ValidTo)
                    {
                        exceptions.Add(
                            new MeteringPointMasterDataInconsistencyException(
                                $"ValidFrom '{next.ValidFrom}' is not equal to previous ValidTo '{acc.Prev.ValidTo}'"));
                    }

                    if (next.Type != acc.Prev.Type)
                    {
                        exceptions.Add(
                            new MeteringPointMasterDataInconsistencyException(
                                $"MeteringPointType '{next.Type}' is not equal to previous MeteringPointType '{acc.Prev.Type}'"));
                    }

                    if (next.Unit != acc.Prev.Unit)
                    {
                        exceptions.Add(
                            new MeteringPointMasterDataInconsistencyException(
                                $"MeasurementUnit '{next.Unit}' is not equal to previous MeasurementUnit '{acc.Prev.Unit}'"));
                    }

                    if (next.ProductId != acc.Prev.ProductId)
                    {
                        exceptions.Add(
                            new MeteringPointMasterDataInconsistencyException(
                                $"ProductId '{next.ProductId}' is not equal to previous ProductId '{acc.Prev.ProductId}'"));
                    }

                    if (next.Identification.Value != acc.Prev.Identification.Value)
                    {
                        exceptions.Add(
                            new MeteringPointMasterDataInconsistencyException(
                                $"Metering point id '{next.Identification.Value} is not equal to previous metering point id '{acc.Prev.Identification.Value}'"));
                    }

                    var (nextFlats, nextExceptions) = GetMeteringPointMasterDataPerEnergySupplier(next);

                    return (next,
                        [.. acc.MeteringPointMasterDataPoints, .. nextFlats],
                        [.. acc.Exceptions, .. exceptions, .. nextExceptions]);
                });

        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                message:
                $"Master data for metering point '{meteringPointId}' in period '{startDate}--{endDate} is inconsistent.",
                innerExceptions: exceptions);
        }

        return meteringPointMasterDataPoints;
    }

    private (IReadOnlyCollection<PMMeteringPointMasterData> Right,
        IReadOnlyCollection<MeteringPointMasterDataInconsistencyException> Left)
        GetMeteringPointMasterDataPerEnergySupplier(
        MeteringPointMasterData meteringPointMasterData)
    {
        var energySupplierStartDate = meteringPointMasterData.EnergySuppliers.MinBy(es => es.StartDate)?.StartDate;
        var energySupplierEndDate = meteringPointMasterData.EnergySuppliers.MaxBy(es => es.EndDate)?.EndDate;

        var exceptions = new List<MeteringPointMasterDataInconsistencyException>();
        if (meteringPointMasterData.EnergySuppliers.Count <= 0)
        {
            exceptions.Add(
                new MeteringPointMasterDataInconsistencyException(
                    $"No energy suppliers found for metering point '{meteringPointMasterData.Identification.Value}' in period {meteringPointMasterData.ValidFrom}--{meteringPointMasterData.ValidTo}."));
        }

        if (meteringPointMasterData.ValidFrom != energySupplierStartDate
            || meteringPointMasterData.ValidTo != energySupplierEndDate)
        {
            exceptions.Add(
                new MeteringPointMasterDataInconsistencyException(
                    $"The interval of the energy suppliers ({energySupplierStartDate}--{energySupplierEndDate}) does not match the master data interval ({meteringPointMasterData.ValidFrom}--{meteringPointMasterData.ValidTo})."));
        }

        if (meteringPointMasterData.EnergySuppliers.Any(
                es => es.Identification.Value != meteringPointMasterData.Identification.Value))
        {
            exceptions.Add(
                new MeteringPointMasterDataInconsistencyException(
                    $"Metering point identification for energy supplier does not match parent identification."));
        }

        return (meteringPointMasterData.EnergySuppliers
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
            .AsReadOnly(),
            exceptions);
    }

    public sealed class MeteringPointMasterDataInconsistencyException(string? message) : Exception(message);
}
