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

using System.Net;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.ProcessManager.Components.IntegrationEventPublisher;
using Energinet.DataHub.ProcessManager.Components.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Components.Tests.Integration.IntegrationEventPublisher.Contracts;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Integration.IntegrationEventPublisher;

public class IntegrationEventPublisherClientTests
    : IClassFixture<IntegrationEventPublisherFixture>, IAsyncLifetime
{
    private readonly IntegrationEventPublisherFixture _fixture;

    public IntegrationEventPublisherClientTests(IntegrationEventPublisherFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _fixture.IntegrationEventListenerMock.ResetMessageHandlersAndReceivedMessages();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Also verifies the response contains JSON in a format that the Health Checks UI supports.
    /// </summary>
    [Fact]
    public async Task Given_IntegrationEventPublisherHealthCheckRegistered_When_CallingReadyEndpoint_Then_ReturnsOkWithExpectedContent()
    {
        // Act
        using var actualResponse = await _fixture.HealthChecksHttpClient.GetAsync($"/monitor/ready");

        // Assert
        using var assertionScope = new AssertionScope();

        actualResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        actualResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var content = await actualResponse.Content.ReadAsStringAsync();
        content.Should().StartWith("{\"status\":\"Healthy\"");
        content.Should().Contain("Integration Event topics");
    }

    [Fact]
    public async Task Given_IntegrationEventMessage_When_PublishingIntegrationEvent_Then_MessageIsPublishedToIntegrationEventTopic()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var eventName = "EventName";
        var minorVersion = 1;

        var expectedMessage = new IntegrationEventTestMessage
        {
            Message = "Test message",
        };

        // Act
        var integrationEventPublisher = _fixture.Provider.GetRequiredService<IIntegrationEventPublisherClient>();
        await integrationEventPublisher.PublishAsync(
            eventIdentification: eventId,
            eventName: eventName,
            eventMinorVersion: minorVersion,
            message: expectedMessage,
            cancellationToken: CancellationToken.None);

        // Assert
        var verifyServiceBusMessage = await _fixture.IntegrationEventListenerMock.When(
            serviceBusMessage =>
            {
                if (serviceBusMessage.Subject == eventName
                    && serviceBusMessage.MessageId == eventId.ToString()
                    && (int)serviceBusMessage.ApplicationProperties["EventMinorVersion"] == minorVersion)
                {
                    var integrationEvent = IntegrationEventTestMessage.Parser.ParseFrom(serviceBusMessage.Body);
                    return integrationEvent.Message == expectedMessage.Message;
                }

                return false;
            })
            .VerifyCountAsync(1);

        var messageReceived = verifyServiceBusMessage.Wait(TimeSpan.FromSeconds(30));
        messageReceived.Should().Be(true, "The integration event message should be published to the integration event topic");
    }
}
