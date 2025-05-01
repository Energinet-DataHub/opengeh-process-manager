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
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.Measurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.Measurements.Mappers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.Measurements.Model;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Azure;
using Moq;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeasurements.Measurements;

public class MeasurementsClientTests
{
    public MeasurementsClientTests()
    {
        var eventHubClientFactory = new Mock<IAzureClientFactory<EventHubProducerClient>>();
        EventHubProducerClientMock = new Mock<EventHubProducerClient>();

        eventHubClientFactory
            .Setup(factory => factory.CreateClient(EventHubProducerClientNames.MeasurementsEventHub))
            .Returns(EventHubProducerClientMock.Object);

        Sut = new MeasurementsClient(eventHubClientFactory.Object);
    }

    internal Mock<EventHubProducerClient> EventHubProducerClientMock { get; }

    internal MeasurementsClient Sut { get; }

    [Fact]
    public async Task SendAsync_WhenCalledWithMeasurements_SendsExpectedDataOnEventHub()
    {
        // Arrange
        var measurements = new MeasurementsForMeteringPoint(
            "test-orchestration-id",
            "test-metering-point-id",
            "test-transaction-id",
            NodaTime.Instant.FromDateTimeUtc(DateTime.UtcNow),
            NodaTime.Instant.FromDateTimeUtc(DateTime.UtcNow),
            NodaTime.Instant.FromDateTimeUtc(DateTime.UtcNow),
            Components.Abstractions.ValueObjects.MeteringPointType.Consumption,
            MeasurementUnit.KilowattHour,
            Components.Abstractions.ValueObjects.Resolution.QuarterHourly,
            [
                new(1, 100, Components.Abstractions.ValueObjects.Quality.AsProvided),
            ]);

        var expectedData = new PersistSubmittedTransaction
        {
            Version = "1",
            OrchestrationInstanceId = measurements.OrchestrationId,
            OrchestrationType = OrchestrationType.OtSubmittedMeasureData,
            MeteringPointId = measurements.MeteringPointId,
            TransactionId = measurements.TransactionId,
            TransactionCreationDatetime = Timestamp.FromDateTimeOffset(measurements.CreatedAt.ToDateTimeOffset()),
            StartDatetime = Timestamp.FromDateTimeOffset(measurements.StartDateTime.ToDateTimeOffset()),
            EndDatetime = Timestamp.FromDateTimeOffset(measurements.EndDateTime.ToDateTimeOffset()),
            MeteringPointType = DataHub.Measurements.Contracts.MeteringPointType.MptConsumption,
            Unit = DataHub.Measurements.Contracts.Unit.UKwh,
            Resolution = DataHub.Measurements.Contracts.Resolution.RPt15M,
        };

        expectedData.Points.Add(
            new DataHub.Measurements.Contracts.Point
            {
                Position = 1,
                Quantity = DecimalValueMapper.Map(100),
                Quality = DataHub.Measurements.Contracts.Quality.QMeasured,
            });

        var expectedEventData = new EventData(expectedData.ToByteArray());

        // Act
        await Sut.SendAsync(measurements, CancellationToken.None);

        // Assert
        EventHubProducerClientMock.Verify(
            client => client.SendAsync(
                It.Is<IReadOnlyList<EventData>>(
                    events => events.Count == 1
                              && events[0].EventBody.ToArray().SequenceEqual(expectedEventData.EventBody.ToArray())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
