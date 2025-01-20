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
using Energinet.DataHub.ProcessManager.Components.Tests.Contracts;
using Energinet.DataHub.ProcessManager.Components.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Integration.Diagnostics;

public class IntegrationEventPublisherExtensionsTests
    : IClassFixture<IntegrationEventPublisherFixture>
{
    private readonly IntegrationEventPublisherFixture _fixture;

    public IntegrationEventPublisherExtensionsTests(IntegrationEventPublisherFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Also verifies the response contains JSON in a format that the Health Checks UI supports.
    /// </summary>
    [Fact]
    public async Task IntegrationEventPublisherHealthCheckRegistered_WhenCallingReadyEndpoint_ReturnOKAndExpectedContent()
    {
        // Act
        using var actualResponse = await _fixture.HttpClient!.GetAsync($"/monitor/ready");

        // Assert
        using var assertionScope = new AssertionScope();

        actualResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        actualResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task IntegrationEventPublisher_WhenPublishingAnEvent_MessageIsPublishedToIntegrationEventTopic()
    {
        // Arrange
        var integrationEventPublisher = _fixture.Provider!.GetRequiredService<IIntegrationEventPublisherClient>();
        var eventId = Guid.NewGuid();
        var eventName = "EventName";
        var minorVersion = 1;

        var message = new IntegrationEventId()
        {
            Id = eventId.ToString(),
        };

        // Act
        await integrationEventPublisher.PublishAsync(
            eventIdentification: eventId,
            eventName: eventName,
            eventMinorVersion: minorVersion,
            message: message,
            cancellationToken: CancellationToken.None);

        // Assert
        var waitForMatch = _fixture.ListenerMock.When(
            serviceBusMessage =>
            {
                if (serviceBusMessage.Subject != eventName
                    || serviceBusMessage.MessageId != eventId.ToString()
                    || (int)serviceBusMessage.ApplicationProperties["EventMinorVersion"] != minorVersion)
                {
                    return false;
                }

                var body = IntegrationEventId.Parser.ParseFrom(serviceBusMessage.Body);

                return body.Equals(message);
            })
            .VerifyCountAsync(1);

        var matchFound = (await waitForMatch.WaitAsync(CancellationToken.None)).IsSet;
        matchFound.Should().Be(true, "The message should be published to the integration event topic");
    }
}
