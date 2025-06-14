﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Mappers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Model;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NodaTime;
using Point = Energinet.DataHub.Measurements.Contracts.Point;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;

public class MeasurementsClient(
    MeasurementsEventHubProducerClientFactory measurementsEventHubProducerClientFactory)
        : IMeasurementsClient
{
    private readonly MeasurementsEventHubProducerClientFactory _measurementsEventHubProducerClientFactory
        = measurementsEventHubProducerClientFactory;

    public async Task SendAsync(
        MeasurementsForMeteringPoint measurementsForMeteringPoint,
        CancellationToken cancellationToken)
    {
        var data = new PersistSubmittedTransaction
        {
            Version = "1",
            OrchestrationInstanceId = measurementsForMeteringPoint.OrchestrationId,
            OrchestrationType = OrchestrationType.OtSubmittedMeasureData,
            MeteringPointId = measurementsForMeteringPoint.MeteringPointId,
            TransactionId = measurementsForMeteringPoint.TransactionId,
            TransactionCreationDatetime = MapDateTime(measurementsForMeteringPoint.CreatedAt),
            StartDatetime = MapDateTime(measurementsForMeteringPoint.StartDateTime),
            EndDatetime = MapDateTime(measurementsForMeteringPoint.EndDateTime),
            MeteringPointType = MeasurementsMapper.MeteringPointType.Map(measurementsForMeteringPoint.MeteringPointType),
            Unit = MeasurementsMapper.MeasurementUnit.Map(measurementsForMeteringPoint.Unit),
            Resolution = MeasurementsMapper.Resolution.Map(measurementsForMeteringPoint.Resolution),
        };

        data.Points.AddRange(
            measurementsForMeteringPoint.Measurements.Select(
                p => new Point
                {
                    Position = p.Position,
                    Quantity = DecimalValueMapper.Map(p.Quantity),
                    Quality = MeasurementsMapper.Quality.Map(p.Quality),
                }));

        // Serialize the data to a byte array.
        var eventData = new EventData(data.ToByteArray());

        var measurementEventHubProducerClient = _measurementsEventHubProducerClientFactory
            .Create(measurementsForMeteringPoint.MeteringPointId);

        await measurementEventHubProducerClient.SendAsync([eventData], cancellationToken).ConfigureAwait(false);
    }

    private static Timestamp MapDateTime(Instant instant) => Timestamp.FromDateTimeOffset(instant.ToDateTimeOffset());
}
