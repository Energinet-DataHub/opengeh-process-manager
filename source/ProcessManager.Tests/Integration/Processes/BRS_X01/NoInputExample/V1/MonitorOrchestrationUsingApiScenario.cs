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

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.NoInputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Tests.Fixtures;
using FluentAssertions;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Tests.Integration.Processes.BRS_X01.NoInputExample.V1;

/// <summary>
/// Test case where we verify the ProcessManager.Example.Orchestrations and Process Manager Api
/// can be used to start an example orchestration (with no-input parameter) and
/// monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ProcessManagerAppCollection))]
public class MonitorOrchestrationUsingApiScenario : IAsyncLifetime
{
    public MonitorOrchestrationUsingApiScenario(
        ProcessManagerAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private ProcessManagerAppFixture Fixture { get; }

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
    public async Task ExampleOrchestration_WhenStartedWithoutToken_ResponseStatusCodeIsUnauthorized()
    {
        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorNumber: ActorNumber.Create("1234567890123"),
            ActorRole: ActorRole.EnergySupplier);

        var command = new StartNoInputExampleCommandV1(userIdentity);

        using var startRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orchestrationinstance/command/start");
        startRequest.Content = new StringContent(
            JsonSerializer.Serialize(command),
            Encoding.UTF8,
            "application/json");

        // Act
        using var response = await Fixture.ProcessManagerAppManager.AppHostManager
            .HttpClient
            .SendAsync(startRequest);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExampleOrchestration_WhenStarted_CanMonitorLifecycle()
    {
        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorNumber: ActorNumber.Create("1234567890123"),
            ActorRole: ActorRole.EnergySupplier);

        var command = new StartNoInputExampleCommandV1(userIdentity);

        using var startRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orchestrationinstance/command/start");
        startRequest.Content = new StringContent(
            JsonSerializer.Serialize(command),
            Encoding.UTF8,
            "application/json");
        startRequest.Headers.Authorization = Fixture.CreateAuthorizationHeader();

        // Step 1: Start new orchestration instance
        using var response = await Fixture.ProcessManagerAppManager.AppHostManager
            .HttpClient
            .SendAsync(startRequest);
        response.EnsureSuccessStatusCode();

        var orchestrationInstanceId = await response.Content
            .ReadFromJsonAsync<Guid>();

        // Step 2: Query until terminated with succeeded
        var query = new GetOrchestrationInstanceByIdQuery(
            userIdentity,
            orchestrationInstanceId);

        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                using var queryRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/orchestrationinstance/query/id");
                queryRequest.Content = new StringContent(
                    JsonSerializer.Serialize(query),
                    Encoding.UTF8,
                    "application/json");
                queryRequest.Headers.Authorization = Fixture.CreateAuthorizationHeader();

                using var queryResponse = await Fixture.ProcessManagerAppManager.AppHostManager
                    .HttpClient
                    .SendAsync(queryRequest);
                queryResponse.EnsureSuccessStatusCode();

                var orchestrationInstance = await queryResponse.Content
                    .ReadFromJsonAsync<OrchestrationInstanceDto>();

                return orchestrationInstance!.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                       && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationState.Succeeded;
            },
            timeLimit: TimeSpan.FromSeconds(40),
            delay: TimeSpan.FromSeconds(2));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }
}
