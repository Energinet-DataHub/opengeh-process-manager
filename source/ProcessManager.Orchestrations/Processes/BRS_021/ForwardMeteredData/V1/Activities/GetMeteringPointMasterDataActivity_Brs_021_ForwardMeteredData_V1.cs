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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

internal sealed class GetMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository,
    IElectricityMarketViews electricityMarketViews)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;

    [Function(nameof(GetMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput activityInput)
    {
        if (activityInput.MeteringPointIdentification is null || activityInput.EndDateTime is null)
        {
            return new([]);
        }

        var id = new MeteringPointIdentification(activityInput.MeteringPointIdentification);
        var startDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.StartDateTime);
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.EndDateTime);

        if (!startDateTime.Success || !endDateTime.Success)
        {
            return new([]);
        }

        var meteringPointMasterDatas = await _electricityMarketViews
            .GetMeteringPointMasterDataChangesAsync(id, new Interval(startDateTime.Value, endDateTime.Value))
            .ToListAsync()
            .ConfigureAwait(false);
        return new(
            meteringPointMasterDatas
                .Select(Map)
                .ToList());
    }

    private static Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointMasterData Map(MeteringPointMasterData arg)
    {
        return new(
            new Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointId(arg.Identification.Value),
            new Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.GridAreaCode(arg.GridAreaCode.Value),
            new Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.ActorNumber(arg.GridAccessProvider.Value),
            MapConnectionState(arg.ConnectionState),
            Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType.FromName(nameof(arg.Type)),
            MapSubType(arg.SubType),
            MapUnit(arg.Unit));
    }

    private static MeasurementUnit MapUnit(MeasureUnit measureUnit)
    {
        switch (measureUnit)
        {
            case MeasureUnit.Ampere:
                return MeasurementUnit.Ampere;
            case MeasureUnit.STK:
                return MeasurementUnit.Pieces;
            case MeasureUnit.kVArh:
                return MeasurementUnit.KiloVoltAmpereReactiveHour;
            case MeasureUnit.kWh:
                return MeasurementUnit.KilowattHour;
            case MeasureUnit.kW:
                return MeasurementUnit.Kilowatt;
            case MeasureUnit.MW:
                return MeasurementUnit.Megawatt;
            case MeasureUnit.MWh:
                return MeasurementUnit.MegawattHour;
            case MeasureUnit.Tonne:
                return MeasurementUnit.MetricTon;
            case MeasureUnit.MVAr:
                return MeasurementUnit.MegaVoltAmpereReactivePower;
            case MeasureUnit.DanishTariffCode:
                return MeasurementUnit.DanishTariffCode;
            default:
                throw new ArgumentOutOfRangeException(nameof(measureUnit), measureUnit, null);
        }
    }

    private static Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType MapSubType(MeteringPointSubType meteringPointSubType)
    {
        switch (meteringPointSubType)
        {
            case MeteringPointSubType.Physical:
                return Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType.Physical;
            case MeteringPointSubType.Virtual:
                return Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType.Virtual;
            case MeteringPointSubType.Calculated:
                return Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType.Calculated;
            default:
                throw new ArgumentOutOfRangeException(nameof(meteringPointSubType), meteringPointSubType, null);
        }
    }

    private static Model.ConnectionState MapConnectionState(ConnectionState connectionState)
    {
        switch (connectionState)
        {
            case ConnectionState.NotUsed:
                return Model.ConnectionState.NotUsed;
            case ConnectionState.ClosedDown:
                return Model.ConnectionState.ClosedDown;
            case ConnectionState.New:
                return Model.ConnectionState.New;
            case ConnectionState.Connected:
                return Model.ConnectionState.Connected;
            case ConnectionState.Disconnected:
                return Model.ConnectionState.Disconnected;
            default:
                throw new ArgumentOutOfRangeException(nameof(connectionState), connectionState, null);
        }
    }

    public sealed record ActivityInput(string? MeteringPointIdentification, string StartDateTime, string? EndDateTime);

    public sealed record ActivityOutput(IReadOnlyCollection<Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointMasterData> MeteringPointMasterData);
}
