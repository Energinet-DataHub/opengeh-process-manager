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
using DurableTask.Core.Serializing;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Api;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using FluentAssertions;
using Moq;

namespace Energinet.DataHub.ProcessManager.Tests.Unit.Api;

public class NotifyOrchestrationInstanceTriggerTests
{
    [Fact]
    public async Task
        Given_NotifyOrchestrationInstanceV1Message_When_Triggered_CanHandleEventDataWithDurableFunctionJsonConverter()
    {
        // Given NotifyOrchestrationInstanceV1 service bus message
        var expectedInstanceId = new OrchestrationInstanceId(Guid.NewGuid());
        var expectedEventName = "TestEvent";
        var notifyOrchestration = new NotifyOrchestrationInstanceV1
        {
            OrchestrationInstanceId = expectedInstanceId.Value.ToString(),
            EventName = expectedEventName,
        };

        var expectedNotifyEventData = new NotifyEventData("Test message");
        notifyOrchestration.SetData(expectedNotifyEventData);
        var serviceBusMessage = notifyOrchestration.ToServiceBusMessage(
            subject: "Test subject",
            idempotencyKey: Guid.NewGuid().ToString());

        var receivedServiceBusMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            subject: serviceBusMessage.Subject,
            messageId: serviceBusMessage.MessageId,
            body: serviceBusMessage.Body,
            contentType: serviceBusMessage.ContentType,
            properties: serviceBusMessage.ApplicationProperties);

        var notifyCommandsMock = new Mock<INotifyOrchestrationInstanceCommands>();
        object? raisedNotifyEventData = null;
        notifyCommandsMock.Setup(
                c =>
                    c.NotifyOrchestrationInstanceAsync(expectedInstanceId, expectedEventName, It.IsAny<object>()))
            .Callback(
                (OrchestrationInstanceId _, string _, object? data) =>
                {
                    raisedNotifyEventData = data;
                });

        var notifyOrchestrationInstanceTrigger = new NotifyOrchestrationInstanceTrigger(notifyCommandsMock.Object);

        // When triggered
        await notifyOrchestrationInstanceTrigger.Run(receivedServiceBusMessage);

        // Then can handle generic event data with Durable Function JSON converter
        var durableFunctionJsonConverter = new JsonDataConverter();

        // => Serialize generic data to JSON string using Durable Function serializer
        var serializedGenericData = durableFunctionJsonConverter.Serialize(raisedNotifyEventData);

        // => When the serialized data is deserialized is is equal to the expected data
        var deserializedNotifyEventData = durableFunctionJsonConverter.Deserialize<NotifyEventData>(serializedGenericData);
        deserializedNotifyEventData.Should().BeEquivalentTo(expectedNotifyEventData);
    }

    private record NotifyEventData(string Message) : INotifyDataDto;
}
