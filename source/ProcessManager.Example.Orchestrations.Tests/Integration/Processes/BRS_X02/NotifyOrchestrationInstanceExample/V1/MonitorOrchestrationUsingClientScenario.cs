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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to notify an example orchestration
/// and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientScenario : IAsyncLifetime
{
    public MonitorOrchestrationUsingClientScenario(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            // Process Manager HTTP client
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),

            // Process Manager message client
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"]
                = Fixture.ProcessManagerTopic.Name,
        });

        // Process Manager HTTP client
        services.AddProcessManagerHttpClients();

        // Process Manager message client
        services.AddAzureClients(b =>
            b.AddServiceBusClientWithNamespace(Fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerMessageClient();

        ServiceProvider = services.BuildServiceProvider();
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.ExampleOrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Tests that we can send a notify event using the <see cref="IProcessManagerMessageClient"/>.
    /// </summary>
    [Fact]
    public async Task NotifyOrchestrationInstanceExample_WhenStarted_CanReceiveExampleNotifyEventWithData()
    {
        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new orchestration instance
        var startRequestCommand = new StartNotifyOrchestrationInstanceExampleCommandV1(
            new ActorIdentityDto(Guid.NewGuid()),
            new NotifyOrchestrationInstanceExampleInputV1(
                InputString: "input-string"),
            IdempotencyKey: Guid.NewGuid().ToString());

        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startRequestCommand,
            CancellationToken.None);

        // Step 2: Query until waiting for ExampleNotifyEvent
        var (isWaitingForNotify, orchestrationInstanceWaitingForEvent) = await processManagerClient
            .TryWaitForOrchestrationInstance<NotifyOrchestrationInstanceExampleInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                comparer: (oi) =>
                {
                    var enqueueActorMessagesStep = oi.Steps
                        .Single(s => s.Sequence == Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1.WaitForExampleNotifyEventStepSequence);

                    return enqueueActorMessagesStep.Lifecycle.State == StepInstanceLifecycleState.Running;
                });

        isWaitingForNotify.Should().BeTrue("because the orchestration instance should wait for an ExampleNotifyEvent");
        orchestrationInstanceWaitingForEvent.Should().NotBeNull();

        // Step 3: Send ExampleNotifyEvent event
        var expectedEventDataMessage = "This is a notification data example";
        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent<ExampleNotifyEventDataV1>(
                OrchestrationInstanceId: orchestrationInstanceWaitingForEvent!.Id.ToString(),
                EventName: NotifyOrchestrationInstanceExampleNotifyEventsV1.ExampleNotifyEvent,
                Data: new ExampleNotifyEventDataV1(expectedEventDataMessage)),
            CancellationToken.None);

        // Step 4: Query until terminated with succeeded
        var (isTerminated, succeededOrchestrationInstance) = await processManagerClient
            .TryWaitForOrchestrationInstance<NotifyOrchestrationInstanceExampleInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                (oi) => oi is
                {
                    Lifecycle:
                    {
                        State: OrchestrationInstanceLifecycleState.Terminated,
                        TerminationState: OrchestrationInstanceTerminationState.Succeeded,
                    },
                });

        isTerminated.Should().BeTrue("because the orchestration instance should complete within given wait time");
        succeededOrchestrationInstance.Should().NotBeNull();

        // Assert that custom status is the expected notify event data
        succeededOrchestrationInstance!.Steps.Should()
            .HaveCount(1)
            .And.ContainSingle(s => s.CustomState == expectedEventDataMessage);
    }

    /// <summary>
    /// Tests that when receiving multiple notify events, only the first one is used (idempotency).
    /// </summary>
    [Fact]
    public async Task NotifyOrchestrationInstanceExample_WhenReceivedMultipleNotifyEvents_OnlyFirstNotifyEventIsUsed()
    {
        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new orchestration instance
        var startRequestCommand = new StartNotifyOrchestrationInstanceExampleCommandV1(
            new ActorIdentityDto(Guid.NewGuid()),
            new NotifyOrchestrationInstanceExampleInputV1(
                InputString: "input-string"),
            IdempotencyKey: Guid.NewGuid().ToString());

        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startRequestCommand,
            CancellationToken.None);

        // Step 2: Query until waiting for ExampleNotifyEvent
        var (isWaitingForNotify, orchestrationInstanceWaitingForEvent) = await processManagerClient
            .TryWaitForOrchestrationInstance<NotifyOrchestrationInstanceExampleInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                comparer: (oi) =>
                {
                    var enqueueActorMessagesStep = oi.Steps
                        .Single(s => s.Sequence == Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1.WaitForExampleNotifyEventStepSequence);

                    return enqueueActorMessagesStep.Lifecycle.State == StepInstanceLifecycleState.Running;
                });

        isWaitingForNotify.Should().BeTrue("because the orchestration instance should wait for an ExampleNotifyEvent");
        orchestrationInstanceWaitingForEvent.Should().NotBeNull();

        // Step 3a: Send first ExampleNotifyEvent event
        var expectedEventDataMessage = "The expected data message";
        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent<ExampleNotifyEventDataV1>(
                OrchestrationInstanceId: orchestrationInstanceWaitingForEvent!.Id.ToString(),
                EventName: NotifyOrchestrationInstanceExampleNotifyEventsV1.ExampleNotifyEvent,
                Data: new ExampleNotifyEventDataV1(expectedEventDataMessage)),
            CancellationToken.None);

        // Step 3b: Send another ExampleNotifyEvent event
        var ignoredEventDataMessage = "An incorrect data message";
        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent<ExampleNotifyEventDataV1>(
                OrchestrationInstanceId: orchestrationInstanceWaitingForEvent.Id.ToString(),
                EventName: NotifyOrchestrationInstanceExampleNotifyEventsV1.ExampleNotifyEvent,
                Data: new ExampleNotifyEventDataV1(ignoredEventDataMessage)),
            CancellationToken.None);

        // Step 4: Query until terminated with succeeded
        var (isTerminated, succeededOrchestrationInstance) = await processManagerClient
            .TryWaitForOrchestrationInstance<NotifyOrchestrationInstanceExampleInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                (oi) => oi is
                {
                    Lifecycle:
                    {
                        State: OrchestrationInstanceLifecycleState.Terminated,
                        TerminationState: OrchestrationInstanceTerminationState.Succeeded,
                    },
                });

        isTerminated.Should().BeTrue("because the orchestration instance should complete within given wait time");
        succeededOrchestrationInstance.Should().NotBeNull();

        // Assert that custom status is the expected notify event data
        succeededOrchestrationInstance!.Steps.Should()
            .HaveCount(1)
            .And.ContainSingle(s => s.CustomState == expectedEventDataMessage)
            .And.NotContain(s => s.CustomState == ignoredEventDataMessage);
    }
}
