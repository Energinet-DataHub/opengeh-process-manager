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

using System.Net;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Monitor;

/// <summary>
/// Tests verifying the configuration and behaviour of Health Checks.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class HealthCheckEndpointTests : IAsyncLifetime
{
    public HealthCheckEndpointTests(OrchestrationsAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private OrchestrationsAppFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.EnsureAppHostUsesMockedDatabricksApi(false);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verify the response contains JSON in a format that the Health Checks UI supports.
    /// </summary>
    [Theory]
    [InlineData("live")]
    [InlineData("ready")]
    [InlineData("status")]
    public async Task FunctionApp_WhenCallingHealthCheck_ReturnOKAndExpectedContent(string healthCheckEndpoint)
    {
        // Arrange
        Fixture.OrchestrationsAppManager.MockServer.MockElectricityMarketHealthCheck();

        // Act
        using var actualResponse = await Fixture.OrchestrationsAppManager.AppHostManager.HttpClient.GetAsync($"api/monitor/{healthCheckEndpoint}");

        // Assert
        using var assertionScope = new AssertionScope();

        actualResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        actualResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var content = await actualResponse.Content.ReadAsStringAsync();
        content.Should().StartWith("{\"status\":\"Healthy\"");
    }
}
