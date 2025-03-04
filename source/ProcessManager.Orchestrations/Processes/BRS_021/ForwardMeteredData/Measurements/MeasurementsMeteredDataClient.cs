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

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Mapper;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Mappers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Model;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Azure;
using NodaTime;
using Point = Energinet.DataHub.Measurements.Contracts.Point;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;

public class MeasurementsMeteredDataClient(
    IAzureClientFactory<EventHubProducerClient> eventHubClientFactory)
        : IMeasurementsMeteredDataClient
{
    private readonly EventHubProducerClient _measurementEventHubProducerClient =
        eventHubClientFactory.CreateClient(EventHubProducerClientNames.MeasurementsEventHub);

    public async Task SendAsync(MeteredDataForMeteringPoint meteredDataForMeteringPoint, CancellationToken cancellationToken)
    {
        var data = new PersistSubmittedTransaction()
        {
            OrchestrationInstanceId = meteredDataForMeteringPoint.OrchestrationId,
            OrchestrationType = OrchestrationType.OtSubmittedMeasureData,
            MeteringPointId = meteredDataForMeteringPoint.MeteringPointId,
            TransactionId = meteredDataForMeteringPoint.TransactionId,
            TransactionCreationDatetime = MapDateTime(meteredDataForMeteringPoint.CreatedAt),
            StartDatetime = MapDateTime(meteredDataForMeteringPoint.StartDateTime),
            EndDatetime = MapDateTime(meteredDataForMeteringPoint.EndDateTime),
            MeteringPointType = MeteredDataToMeasurementMapper.MeteringPointType.Map(meteredDataForMeteringPoint.MeteringPointType),
            Product = meteredDataForMeteringPoint.Product,
            Unit = MeteredDataToMeasurementMapper.MeasurementUnit.Map(meteredDataForMeteringPoint.Unit),
            Resolution = MeteredDataToMeasurementMapper.Resolution.Map(meteredDataForMeteringPoint.Resolution),
        };

        data.Points.AddRange(meteredDataForMeteringPoint.Points.Select(p => new Point()
        {
            Position = p.Position,
            Quantity = DecimalValueMapper.Map(p.Quantity),
            Quality = MeteredDataToMeasurementMapper.Quality.Map(p.Quality),
        }));

        // Serialize the data to a byte array
        var eventData = new EventData(data.ToByteArray());
        await _measurementEventHubProducerClient.SendAsync([eventData], cancellationToken).ConfigureAwait(false);
    }

    private Timestamp MapDateTime(Instant instant)
    {
        return Timestamp.FromDateTimeOffset(instant.ToDateTimeOffset());
    }
}
