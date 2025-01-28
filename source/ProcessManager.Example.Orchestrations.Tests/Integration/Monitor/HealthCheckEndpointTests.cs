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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X04_OrchestrationDescriptionBreakingChanges;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Monitor;

/// <summary>
/// Tests verifying the configuration and behaviour of Health Checks.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class HealthCheckEndpointTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _testOutputHelper;

    public HealthCheckEndpointTests(ExampleOrchestrationsAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        Fixture = fixture;
        Fixture.SetTestOutputHelper(_testOutputHelper);
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

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
        // Act
        using var actualResponse = await Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient.GetAsync($"api/monitor/{healthCheckEndpoint}");

        // Assert
        using var assertionScope = new AssertionScope();

        actualResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        actualResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var content = await actualResponse.Content.ReadAsStringAsync();
        content.Should().StartWith("{\"status\":\"Healthy\"");
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionBreakingChanges_When_CallingHealthCheck_Then_IsUnhealthy()
    {
        var uniqueName = BreakingChangesOrchestrationDescriptionBuilder.UniqueName;
        await using (var dbContext = Fixture.ProcessManagerAppManager.DatabaseManager.CreateDbContext())
        {
            // Change to existing orchestration description so there is breaking changes next time synchronization is ran
            // (orchestration register synchronization runs at application startup)
            var orchestrationDescription = await dbContext
                .OrchestrationDescriptions
                .FirstAsync(od => od.UniqueName == uniqueName);
            orchestrationDescription.FunctionName = "Breaking change!";
            orchestrationDescription.AppendStepDescription("Breaking change!");
            await dbContext.SaveChangesAsync();
        }

        // Restart app to perform synchronization again
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.RestartHost();

        using var healthCheckResponse = await Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient
            .GetAsync($"api/monitor/ready");

        // Assert
        using var assertionScope = new AssertionScope();

        var hostLogs = Fixture.ExampleOrchestrationsAppManager.AppHostManager.GetHostLogSnapshot();

        const string breakingChangesNotAllowedString = "Breaking changes to orchestration description are not allowed";
        const string changedPropertiesString = "ChangedProperties=Steps,FunctionName";
        hostLogs.Should().ContainMatch($"*{breakingChangesNotAllowedString}*");
        hostLogs.Should().ContainMatch($"*{changedPropertiesString}*");

        healthCheckResponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var healthCheckContent = await healthCheckResponse.Content.ReadAsStringAsync();
        healthCheckContent.Should().StartWith("{\"status\":\"Unhealthy\"");
        healthCheckContent.Should().Contain(breakingChangesNotAllowedString);
        healthCheckContent.Should().Contain(changedPropertiesString);
    }
}
