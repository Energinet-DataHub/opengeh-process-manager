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

using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Components.Extensions.Mapper;
using Energinet.DataHub.ProcessManager.Components.Measurements.Models;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NodaTime;
using Point = Energinet.DataHub.Measurements.Contracts.Point;
using Resolution = Energinet.DataHub.Measurements.Contracts.Resolution;

namespace Energinet.DataHub.ProcessManager.Components.Measurements;

public class MeteredDataClient(EventHubProducerClient eventHubProducerClient) : IMeteredDataClient
{
    private static readonly Dictionary<ProcessManager.Components.Models.MeteringPointType, MeteringPointType> _meteringPointTypeMap = new()
    {
        { ProcessManager.Components.Models.MeteringPointType.Consumption, MeteringPointType.MptConsumption },
        { ProcessManager.Components.Models.MeteringPointType.Production, MeteringPointType.MptProduction },
        { ProcessManager.Components.Models.MeteringPointType.Exchange, MeteringPointType.MptExchange },
        { ProcessManager.Components.Models.MeteringPointType.VeProduction, MeteringPointType.MptVeProduction },
        { ProcessManager.Components.Models.MeteringPointType.Analysis, MeteringPointType.MptAnalysis },
        { ProcessManager.Components.Models.MeteringPointType.NotUsed, MeteringPointType.MptNotUsed },
        { ProcessManager.Components.Models.MeteringPointType.SurplusProductionGroup6, MeteringPointType.MptSurplusProductionGroup6 },
        { ProcessManager.Components.Models.MeteringPointType.NetProduction, MeteringPointType.MptNetProduction },
        { ProcessManager.Components.Models.MeteringPointType.SupplyToGrid, MeteringPointType.MptSupplyToGrid },
        { ProcessManager.Components.Models.MeteringPointType.ConsumptionFromGrid, MeteringPointType.MptConsumptionFromGrid },
        { ProcessManager.Components.Models.MeteringPointType.WholesaleServicesInformation, MeteringPointType.MptWholesaleServicesInformation },
        { ProcessManager.Components.Models.MeteringPointType.OwnProduction, MeteringPointType.MptOwnProduction },
        { ProcessManager.Components.Models.MeteringPointType.NetFromGrid, MeteringPointType.MptNetFromGrid },
        { ProcessManager.Components.Models.MeteringPointType.NetToGrid, MeteringPointType.MptNetToGrid },
        { ProcessManager.Components.Models.MeteringPointType.TotalConsumption, MeteringPointType.MptTotalConsumption },
        { ProcessManager.Components.Models.MeteringPointType.NetLossCorrection, MeteringPointType.MptNetLossCorrection },
        { ProcessManager.Components.Models.MeteringPointType.ElectricalHeating, MeteringPointType.MptElectricalHeating },
        { ProcessManager.Components.Models.MeteringPointType.NetConsumption, MeteringPointType.MptNetConsumption },
        { ProcessManager.Components.Models.MeteringPointType.OtherConsumption, MeteringPointType.MptOtherConsumption },
        { ProcessManager.Components.Models.MeteringPointType.OtherProduction, MeteringPointType.MptOtherProduction },
        { ProcessManager.Components.Models.MeteringPointType.EffectPayment, MeteringPointType.MptEffectPayment },
        { ProcessManager.Components.Models.MeteringPointType.ExchangeReactiveEnergy, MeteringPointType.MptExchangeReactiveEnergy },
        { ProcessManager.Components.Models.MeteringPointType.CollectiveNetProduction, MeteringPointType.MptCollectiveNetProduction },
        { ProcessManager.Components.Models.MeteringPointType.CollectiveNetConsumption, MeteringPointType.MptCollectiveNetConsumption },
    };

    private static readonly Dictionary<ProcessManager.Components.Models.Resolution, Resolution> _resolutionMap = new()
    {
        { ProcessManager.Components.Models.Resolution.QuarterHourly, Resolution.RPt15M },
        { ProcessManager.Components.Models.Resolution.Hourly, Resolution.RPt1H },
    };

    private static readonly Dictionary<ProcessManager.Components.Models.MeasurementUnit, Unit> _measurementUnitMap = new()
    {
        { ProcessManager.Components.Models.MeasurementUnit.KilowattHour, Unit.UKwh },
        { ProcessManager.Components.Models.MeasurementUnit.MegawattHour, Unit.UMwh },
        { ProcessManager.Components.Models.MeasurementUnit.MegaVoltAmpereReactivePower, Unit.UMvarh },
        { ProcessManager.Components.Models.MeasurementUnit.KiloVoltAmpereReactiveHour, Unit.UKvarh },
        { ProcessManager.Components.Models.MeasurementUnit.Kilowatt, Unit.UKw },
        // { ProcessManager.Components.Models.MeasurementUnit.T, Unit.UT }, TODO: hvad er Unit.UT?
        // { ProcessManager.Components.Models.MeasurementUnit.K3, Unit.UK3 }, TODO: Unit.UK3 ikke det samme som Unit.UKvarh?
    };

    private static readonly Dictionary<ProcessManager.Components.Models.Quality, Quality> _qualityMap = new()
    {
        { ProcessManager.Components.Models.Quality.NotAvailable, Quality.QMissing },
        { ProcessManager.Components.Models.Quality.Estimated, Quality.QEstimated },
        { ProcessManager.Components.Models.Quality.AsProvided, Quality.QMeasured },
        { ProcessManager.Components.Models.Quality.Calculated, Quality.QCalculated },
    };

    private readonly EventHubProducerClient _eventHubProducerClient = eventHubProducerClient;

    public async Task SendAsync(MeteredDataForMeasurementPoint meteredDataForMeasurementPoint, CancellationToken cancellationToken)
    {
        var data = new PersistSubmittedTransaction()
        {
            OrchestrationInstanceId = meteredDataForMeasurementPoint.OrchestrationId,
            OrchestrationType = OrchestrationType.OtSubmittedMeasureData,
            MeteringPointId = meteredDataForMeasurementPoint.MeteringPointId,
            TransactionId = meteredDataForMeasurementPoint.TransactionId,
            TransactionCreationDatetime = MapDateTime(meteredDataForMeasurementPoint.CreatedAt),
            StartDatetime = MapDateTime(meteredDataForMeasurementPoint.StartDateTime),
            EndDatetime = MapDateTime(meteredDataForMeasurementPoint.EndDateTime),
            MeteringPointType = _meteringPointTypeMap.Map(meteredDataForMeasurementPoint.MeteringPointType),
            Product = meteredDataForMeasurementPoint.Product,
            Unit = _measurementUnitMap.Map(meteredDataForMeasurementPoint.Unit),
            Resolution = _resolutionMap.Map(meteredDataForMeasurementPoint.Resolution),
        };

        data.Points.AddRange(meteredDataForMeasurementPoint.Points.Select(p => new Point()
        {
            Position = p.Position,
            Quantity = p.Quantity,
            Quality = _qualityMap.Map(p.Quality),
        }));

        // Serialize the data to a byte array
        var eventData = new EventData(data.ToByteArray());
        await _eventHubProducerClient.SendAsync([eventData], cancellationToken).ConfigureAwait(false);
    }

    private Timestamp MapDateTime(Instant instant)
    {
        return Timestamp.FromDateTimeOffset(instant.ToDateTimeOffset());
    }
}
