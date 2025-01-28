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

using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// forward metered data orchestration and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private readonly OrchestrationsAppFixture _fixture;

    public MonitorOrchestrationUsingClientsScenario(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"]
                = _fixture.ProcessManagerTopicName,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = _fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = _fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
        });
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(_fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerMessageClient();
        services.AddProcessManagerHttpClients();
        ServiceProvider = services.BuildServiceProvider();
    }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        _fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        _fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();
        _fixture.EventHubListener.Reset();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    [Fact(Skip = "Because Electricity Market is not enabled.")]
    public async Task ForwardMeteredData_WhenStartedUsingCorrectInput_ThenExecutedHappyPath()
    {
        // Arrange
        var input = CreateMeteredDataForMeteringPointMessageInputV1();

        var startCommand = new StartForwardMeteredDataCommandV1(
            new ActorIdentityDto(input.AuthenticatedActorId),
            input,
            idempotencyKey: Guid.NewGuid().ToString());

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();

        // Act
        var orchestrationCreatedAfter = DateTime.UtcNow.AddSeconds(-1);
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(startCommand, CancellationToken.None);

        // Assert
        var orchestration = await _fixture.DurableClient.WaitForOrchestationStartedAsync(
            orchestrationCreatedAfter,
            name: nameof(Orchestration_Brs_021_ForwardMeteredData_V1));
        var inputToken = JToken.FromObject(input);
        orchestration.Input.ToString().Should().BeEquivalentTo(inputToken.ToString(Newtonsoft.Json.Formatting.None));

        var completeOrchestrationStatus = await _fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestration.InstanceId);

        // => Assert expected history
        using var assertionScope = new AssertionScope();

        var activities = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList();

        activities.Should()
            .NotBeNull()
            .And.Equal(
                new OrchestrationHistoryItem(
                    "ExecutionStarted",
                    FunctionName: "Orchestration_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "OrchestrationInitializeActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "TransitionStepToRunningActivity_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "FindReceiversActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "TransitionStepToTerminatedActivity_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "OrchestrationTerminateActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem("ExecutionCompleted"));

        // => Verify that the durable function completed successfully
        var last = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Last();
        last.Value<string>("EventType").Should().Be("ExecutionCompleted");
        last.Value<string>("Result").Should().Be("Success");
    }

    [Fact(Skip = "Because flow is not implemented.")]
    public async Task ForwardMeteredData_WhenStartedWithFaultyInput_ThenExecutedErrorPath()
    {
        // Arrange
        var input = CreateMeteredDataForMeteringPointMessageInputV1(true);

        var startCommand = new StartForwardMeteredDataCommandV1(
            new ActorIdentityDto(input.AuthenticatedActorId),
            input,
            "test-message-id");

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();

        var orchestrationCreatedAfter = DateTime.UtcNow.AddSeconds(-5);

        // Act
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(startCommand, CancellationToken.None);

        var orchestration = await _fixture.DurableClient.WaitForOrchestationStartedAsync(
            orchestrationCreatedAfter,
            name: "Orchestration_Brs_021_ForwardMeteredData_V1");

        var inputToken = JToken.FromObject(input);
        orchestration.Input.ToString().Should().BeEquivalentTo(inputToken.ToString(Newtonsoft.Json.Formatting.None));

        var completeOrchestrationStatus = await _fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestration.InstanceId);

        // => Assert expected history
        using var assertionScope = new AssertionScope();

        var activities = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList();

        activities.Should()
            .NotBeNull()
            .And.Equal(
                new OrchestrationHistoryItem(
                    "ExecutionStarted",
                    FunctionName: "Orchestration_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "OrchestrationInitializeActivity_Brs_021_ForwardMeteredData_V1"),
                // new OrchestrationHistoryItem(
                //     "TaskCompleted",
                //     FunctionName: "GetMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "PerformValidationActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "ValidationStepTerminateActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "CreateRejectMessageActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "EnqueueRejectMessageActivity_Brs_021_V1"),
                new OrchestrationHistoryItem("TimerCreated"),
                new OrchestrationHistoryItem("TimerFired"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "EnqueueActorMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem(
                    "TaskCompleted",
                    FunctionName: "OrchestrationTerminateActivity_Brs_021_ForwardMeteredData_V1"),
                new OrchestrationHistoryItem("ExecutionCompleted"));

        // => Verify that the durable function completed successfully
        var last = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Last();
        last.Value<string>("EventType").Should().Be("ExecutionCompleted");
        last.Value<string>("Result").Should().Be("Success");
    }

    /// <summary>
    /// Showing how we can orchestrate and monitor an orchestration instance only using clients.
    /// </summary>
    [Fact]
    public async Task ForwardMeteredData_WhenStarted_CanMonitorLifecycle()
    {
        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new orchestration instance
        var input = CreateMeteredDataForMeteringPointMessageInputV1();

        var startCommand = new StartForwardMeteredDataCommandV1(
            new ActorIdentityDto(input.AuthenticatedActorId),
            input,
            idempotencyKey: Guid.NewGuid().ToString());

        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startCommand,
            CancellationToken.None);

        // Step 2: Query until terminated with succeeded
        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await processManagerClient
                    .GetOrchestrationInstanceByIdempotencyKeyAsync<MeteredDataForMeteringPointMessageInputV1>(
                        new GetOrchestrationInstanceByIdempotencyKeyQuery(
                            userIdentity,
                            startCommand.IdempotencyKey),
                        CancellationToken.None);

                return
                    orchestrationInstance != null
                    && orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationState.Succeeded;
            },
            timeLimit: TimeSpan.FromSeconds(20),
            delay: TimeSpan.FromSeconds(3));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }

    private static MeteredDataForMeteringPointMessageInputV1 CreateMeteredDataForMeteringPointMessageInputV1(
        bool withError = false)
    {
        var input = new MeteredDataForMeteringPointMessageInputV1(
            Guid.NewGuid(),
            new MarketActorRecipient("1111111111111", ActorRole.GridAccessProvider),
            "EGU9B8E2630F9CB4089BDE22B597DFA4EA5",
            withError ? "NoMasterData" : "571313101700011887",
            "D20",
            "8716867000047",
            "K3",
            "2024-12-03T08:00:00Z",
            "PT1H",
            "2024-12-01T23:00Z",
            "2024-12-02T23:00:00Z",
            "5790002606892",
            null,
            new List<EnergyObservation>()
            {
                new("1", "112.000", "A04"),
                new("2", "112.000", "A04"),
                new("3", "112.000", "A04"),
                new("4", "112.000", "A04"),
                new("5", "112.000", "A04"),
                new("6", "112.000", "A04"),
                new("7", "112.000", "A04"),
                new("8", "112.000", "A04"),
                new("9", "112.000", "A04"),
                new("10", "112.000", "A04"),
                new("12", "112.000", "A04"),
                new("12", "112.000", "A04"),
                new("13", "112.000", "A04"),
                new("14", "112.000", "A04"),
                new("15", "112.000", "A04"),
                new("16", "112.000", "A04"),
                new("18", "112.000", "A04"),
                new("19", "112.000", "A04"),
                new("20", "112.000", "A04"),
                new("21", "112.000", "A04"),
                new("22", "112.000", "A04"),
                new("23", "112.000", "A04"),
                new("24", "112.000", "A04"),
            });
        return input;
    }

    public record OrchestrationHistoryItem(
        string? EventType,
        string? Name = null,
        string? FunctionName = null);
}
