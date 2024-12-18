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

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_X01.NoInputExample.V1.Model;
using Energinet.DataHub.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using FluentAssertions;
using Xunit.Abstractions;

namespace Energinet.DataHub.Example.Orchestrations.Tests.Integration.Processes.BRS_X01.NoInputExample;

/// <summary>
/// Test case where we verify the Example.Orchestrations and Process Manager Api
/// can be used to start an example orchestration (with input parameter) and
/// monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class MonitorNoInputExampleUsingApiScenario : IAsyncLifetime
{
    public MonitorNoInputExampleUsingApiScenario(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.ExampleOrchestrationsAppManager.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task NoInputExampleOrchestration_WhenStarted_CanMonitorLifecycle()
    {
        var orchestration = new Brs_X01_NoInputExample_V1();

        var command = new StartNoInputExampleComandV1(
             operatingIdentity: new UserIdentityDto(
                 Guid.NewGuid(),
                 Guid.NewGuid()));

        using var scheduleRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orchestrationinstance/command/start/{orchestration.Name}/{orchestration.Version}");
        scheduleRequest.Content = new StringContent(
            JsonSerializer.Serialize(command),
            Encoding.UTF8,
            "application/json");

        // Step 1: Start new orchestration instance
        using var response = await Fixture.ProcessManagerAppManager.AppHostManager
            .HttpClient
            .SendAsync(scheduleRequest);
        response.EnsureSuccessStatusCode();

        var orchestrationInstanceId = await response.Content
            .ReadFromJsonAsync<Guid>();

        // Step 2: Query until terminated with succeeded
        var getRequest = new GetOrchestrationInstanceByIdQuery(
            new UserIdentityDto(
                Guid.NewGuid(),
                Guid.NewGuid()),
            orchestrationInstanceId);

        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                using var queryRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/orchestrationinstance/query/id");
                queryRequest.Content = new StringContent(
                    JsonSerializer.Serialize(getRequest),
                    Encoding.UTF8,
                    "application/json");

                using var queryResponse = await Fixture.ProcessManagerAppManager.AppHostManager
                    .HttpClient
                    .SendAsync(queryRequest);
                queryResponse.EnsureSuccessStatusCode();

                var orchestrationInstance = await queryResponse.Content
                    .ReadFromJsonAsync<OrchestrationInstanceDto>();

                return orchestrationInstance!.Lifecycle.State == OrchestrationInstanceLifecycleStates.Terminated;
            },
            timeLimit: TimeSpan.FromSeconds(40),
            delay: TimeSpan.FromSeconds(2));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }
}
