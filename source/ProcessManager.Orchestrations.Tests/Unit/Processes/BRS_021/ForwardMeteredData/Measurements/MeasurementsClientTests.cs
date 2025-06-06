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
using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Mappers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Model;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.Measurements;

public class MeasurementsClientTests
{
    public MeasurementsClientTests()
    {
        var optionsMock = new Mock<IOptions<ProcessManagerComponentsOptions>>();
        optionsMock
            .Setup(o => o.Value)
            .Returns(new ProcessManagerComponentsOptions
            {
                AllowMockDependenciesForTests = false,
            });

        var eventHubClientFactory = new Mock<IAzureClientFactory<EventHubProducerClient>>();
        EventHubProducerClientMock = new Mock<EventHubProducerClient>();

        eventHubClientFactory
            .Setup(factory => factory.CreateClient(EventHubProducerClientNames.MeasurementsEventHub))
            .Returns(EventHubProducerClientMock.Object);

        var measurementsEventHubProducerClientFactory = new MeasurementsEventHubProducerClientFactory(
            optionsMock.Object,
            eventHubClientFactory.Object);

        Sut = new MeasurementsClient(measurementsEventHubProducerClientFactory);
    }

    internal Mock<EventHubProducerClient> EventHubProducerClientMock { get; }

    internal MeasurementsClient Sut { get; }

    [Fact]
    public async Task SendAsync_WhenCalledWithMeteredData_SendsExpectedDataOnEventHub()
    {
        // Arrange
        var meteredData = new MeasurementsForMeteringPoint(
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
            OrchestrationInstanceId = meteredData.OrchestrationId,
            OrchestrationType = OrchestrationType.OtSubmittedMeasureData,
            MeteringPointId = meteredData.MeteringPointId,
            TransactionId = meteredData.TransactionId,
            TransactionCreationDatetime = Timestamp.FromDateTimeOffset(meteredData.CreatedAt.ToDateTimeOffset()),
            StartDatetime = Timestamp.FromDateTimeOffset(meteredData.StartDateTime.ToDateTimeOffset()),
            EndDatetime = Timestamp.FromDateTimeOffset(meteredData.EndDateTime.ToDateTimeOffset()),
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
