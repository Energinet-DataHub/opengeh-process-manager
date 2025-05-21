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

using Azure.Messaging.ServiceBus;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Google.Protobuf;
using Moq;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Unit.Application.Handlers;

public class StartOrchestrationInstanceFromMessageHandlerTests
{
    [Fact]
    public async Task Given_ValidMajorVersion_When_HandleAsync_Then_DelegatesToCorrectHandler()
    {
        // Arrange
        var validMajorVersion = nameof(StartOrchestrationInstanceV1);

        var handlerMock = new Mock<IStartOrchestrationInstanceHandler>();
        var startOrchestrationInstance = new StartOrchestrationInstanceV1();

        var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            subject: "Brs_021_SendMeasurements",
            messageId: Guid.NewGuid().ToString(),
            body: new BinaryData(JsonFormatter.Default.Format(startOrchestrationInstance)),
            contentType: "application/json",
            properties: new Dictionary<string, object>
            {
                { "MajorVersion", validMajorVersion },
                { "BodyFormat", "Json" },
            });

        handlerMock.Setup(h => h.CanHandle(startOrchestrationInstance)).Returns(true);

        var handlers = new[] { handlerMock.Object };
        var sut = new StartOrchestrationInstanceFromMessageHandler(handlers);

        // Act
        await sut.HandleAsync(serviceBusReceivedMessage);

        // Assert
        handlerMock.Verify(
            h => h.HandleAsync(
                startOrchestrationInstance,
                It.IsAny<IdempotencyKey>()),
            Times.Once);
    }

    [Fact]
    public async Task Given_InvalidMajorVersion_When_HandleAsync_Then_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidMajorVersion = "invalidMajorVersion";
        var handlerMock = new Mock<IStartOrchestrationInstanceHandler>();
        var startOrchestrationInstance = new StartOrchestrationInstanceV1();

        var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            subject: "Brs_021_SendMeasurements",
            messageId: Guid.NewGuid().ToString(),
            body: new BinaryData(JsonFormatter.Default.Format(startOrchestrationInstance)),
            contentType: "application/json",
            properties: new Dictionary<string, object>
            {
                { "MajorVersion", invalidMajorVersion },
                { "BodyFormat", "Json" },
            });

        handlerMock.Setup(h => h.CanHandle(startOrchestrationInstance)).Returns(true);

        var handlers = new[] { handlerMock.Object };
        var sut = new StartOrchestrationInstanceFromMessageHandler(handlers);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.HandleAsync(serviceBusReceivedMessage));
    }
}
