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
using Azure.Identity;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.IntegrationEventPublisher;
using Energinet.DataHub.ProcessManager.Components.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
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
    public async Task DatabricksJobsHealthCheckRegistered_WhenCallingReadyEndpoint_ReturnOKAndExpectedContent()
    {
        // Act
        using var actualResponse = await _fixture.HttpClient!.GetAsync($"/monitor/ready");

        // Assert
        using var assertionScope = new AssertionScope();

        actualResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        actualResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    // [Fact]
    // public void AddIntegrationEventPublisher_WhenSectionIsFilledOut_RegistrationsArePerformed()
    // {
    //     // Arrange
    //     Services.AddInMemoryConfiguration(
    //         new Dictionary<string, string?>()
    //         {
    //             [$"{IntegrationEventsOptions.SectionName}:{nameof(IntegrationEventsOptions.TopicName)}"] =
    //                 FakeTopic,
    //             [$"{IntegrationEventsOptions.SectionName}:{nameof(IntegrationEventsOptions.SubscriptionName)}"] =
    //                 FakeSubscription,
    //         });
    //
    //     Services.AddIntegrationEventPublisher(new DefaultAzureCredential());
    //
    //     // Assert
    //     var serviceProvider = Services.BuildServiceProvider();
    //
    //     var actualClient = serviceProvider.GetRequiredService<IIntegrationEventPublisherClient>();
    //     actualClient.Should().BeOfType<IIntegrationEventPublisherClient>();
    // }
}
