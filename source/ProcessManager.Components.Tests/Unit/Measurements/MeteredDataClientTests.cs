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
using Energinet.DataHub.ProcessManager.Components.Measurements;
using Energinet.DataHub.ProcessManager.Components.Measurements.Models;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Moq;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Measurements;

public class MeteredDataClientTests
{
    public MeteredDataClientTests()
    {
        EventHubProducerClientMock = new Mock<EventHubProducerClient>();
        Sut = new MeteredDataClient(EventHubProducerClientMock.Object);
    }

    internal Mock<EventHubProducerClient> EventHubProducerClientMock { get; }

    internal MeteredDataClient Sut { get; }

    [Fact]
    public async Task SendAsync_WhenCalledWithMeteredData_SendsExpectedDataOnEventHub()
    {
        // Arrange
        var meteredData = new MeteredDataForMeasurementPoint(
            "test-orchestration-id",
            "test-metering-point-id",
            "test-transaction-id",
            NodaTime.Instant.FromDateTimeUtc(DateTime.UtcNow),
            NodaTime.Instant.FromDateTimeUtc(DateTime.UtcNow),
            NodaTime.Instant.FromDateTimeUtc(DateTime.UtcNow),
            Models.MeteringPointType.Consumption,
            "test-product",
            Models.MeasurementUnit.KilowattHour,
            Models.Resolution.QuarterHourly,
            new List<Energinet.DataHub.ProcessManager.Components.Measurements.Models.Point>
            {
                new(1, 100, Models.Quality.AsProvided),
            });

        var expectedData = new PersistSubmittedTransaction
        {
            OrchestrationInstanceId = meteredData.OrchestrationId,
            OrchestrationType = OrchestrationType.OtSubmittedMeasureData,
            MeteringPointId = meteredData.MeteringPointId,
            TransactionId = meteredData.TransactionId,
            TransactionCreationDatetime = Timestamp.FromDateTimeOffset(meteredData.CreatedAt.ToDateTimeOffset()),
            StartDatetime = Timestamp.FromDateTimeOffset(meteredData.StartDateTime.ToDateTimeOffset()),
            EndDatetime = Timestamp.FromDateTimeOffset(meteredData.EndDateTime.ToDateTimeOffset()),
            MeteringPointType = MeteringPointType.MptConsumption,
            Product = meteredData.Product,
            Unit = Energinet.DataHub.Measurements.Contracts.Unit.UKwh,
            Resolution = Resolution.RPt15M,
        };

        expectedData.Points.Add(new Energinet.DataHub.Measurements.Contracts.Point { Position = 1, Quantity = 100, Quality = Quality.QMeasured });

        var expectedEventData = new EventData(expectedData.ToByteArray());

        // Act
        await Sut.SendAsync(meteredData, CancellationToken.None);

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
