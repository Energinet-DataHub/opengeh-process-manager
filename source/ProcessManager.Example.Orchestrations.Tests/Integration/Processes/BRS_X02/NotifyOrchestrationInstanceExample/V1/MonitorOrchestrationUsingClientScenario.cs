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

using System.Text.Json;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to notify an example orchestration
/// and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientScenario : IAsyncLifetime
{
    private readonly ActorIdentityDto _actorIdentity = new ActorIdentityDto(
        ActorNumber: ActorNumber.Create("1234567891234"),
        ActorRole: ActorRole.EnergySupplier);

    public MonitorOrchestrationUsingClientScenario(
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

    /// <summary>
    /// Tests that we can send a notify event using the <see cref="IProcessManagerMessageClient"/>.
    /// </summary>
    [Fact]
    public async Task NotifyOrchestrationInstanceExample_WhenStarted_CanReceiveExampleNotifyEventWithData()
    {
        // Step 1: Start new orchestration instance
        var startRequestCommand = new StartNotifyOrchestrationInstanceExampleCommandV1(
            _actorIdentity,
            new NotifyOrchestrationInstanceExampleInputV1(
                InputString: "input-string"),
            IdempotencyKey: Guid.NewGuid().ToString(),
            ActorMessageId: Guid.NewGuid().ToString(),
            TransactionId: Guid.NewGuid().ToString());

        await Fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startRequestCommand,
            CancellationToken.None);

        // Step 2: Query until waiting for ExampleNotifyEvent
        var (isWaitingForNotify, orchestrationInstanceWaitingForEvent) = await Fixture.ProcessManagerClient
            .TryWaitForOrchestrationInstance<NotifyOrchestrationInstanceExampleInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                comparer: (oi) =>
                {
                    var enqueueActorMessagesStep = oi.Steps
                        .Single(s => s.Sequence == WaitForNotifyEventStep.StepSequence);

                    return enqueueActorMessagesStep.Lifecycle.State == StepInstanceLifecycleState.Running;
                });

        isWaitingForNotify.Should().BeTrue("because the orchestration instance should wait for an ExampleNotifyEvent");
        orchestrationInstanceWaitingForEvent.Should().NotBeNull();
        orchestrationInstanceWaitingForEvent!.ActorMessageId.Should().Be(startRequestCommand.ActorMessageId);
        orchestrationInstanceWaitingForEvent.TransactionId.Should().Be(startRequestCommand.TransactionId);
        orchestrationInstanceWaitingForEvent.MeteringPointId.Should().BeNull();

        // Step 3: Send ExampleNotifyEvent event
        const string expectedEventDataMessage = "This is a notification data example";
        await Fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceExampleNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstanceWaitingForEvent!.Id.ToString(),
                Data: new ExampleNotifyEventDataV1(expectedEventDataMessage)),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (isTerminated, orchestrationInstance) = await Fixture.ProcessManagerClient
            .TryWaitForOrchestrationInstance<NotifyOrchestrationInstanceExampleInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                (oi) => oi is
                {
                    Lifecycle:
                    {
                        State: OrchestrationInstanceLifecycleState.Terminated,
                    },
                });

        isTerminated.Should().BeTrue("because the orchestration instance should complete within given wait time");
        orchestrationInstance.Should().NotBeNull();

        orchestrationInstance!.Lifecycle.TerminationState.Should().Be(OrchestrationInstanceTerminationState.Succeeded);

        var expectedCustomState = JsonSerializer.Serialize(new WaitForNotifyEventStep.CustomState(expectedEventDataMessage));
        orchestrationInstance.Steps.Should()
            .HaveCount(1)
            .And.ContainSingle(s => s.CustomState == expectedCustomState);
    }

    /// <summary>
    /// Tests that when receiving multiple notify events, only the first one is used (idempotency).
    /// </summary>
    [Fact]
    public async Task NotifyOrchestrationInstanceExample_WhenReceivedMultipleNotifyEvents_OnlyFirstNotifyEventIsUsed()
    {
        // Step 1: Start new orchestration instance
        var startRequestCommand = new StartNotifyOrchestrationInstanceExampleCommandV1(
            _actorIdentity,
            new NotifyOrchestrationInstanceExampleInputV1(
                InputString: "input-string"),
            IdempotencyKey: Guid.NewGuid().ToString(),
            ActorMessageId: Guid.NewGuid().ToString(),
            TransactionId: Guid.NewGuid().ToString());

        await Fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startRequestCommand,
            CancellationToken.None);

        // Step 2: Query until waiting for ExampleNotifyEvent
        var (isWaitingForNotify, orchestrationInstanceWaitingForEvent) = await Fixture.ProcessManagerClient
            .TryWaitForOrchestrationInstance<NotifyOrchestrationInstanceExampleInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                comparer: (oi) =>
                {
                    var enqueueActorMessagesStep = oi.Steps
                        .Single(s => s.Sequence == WaitForNotifyEventStep.StepSequence);

                    return enqueueActorMessagesStep.Lifecycle.State == StepInstanceLifecycleState.Running;
                });

        isWaitingForNotify.Should().BeTrue("because the orchestration instance should wait for an ExampleNotifyEvent");
        orchestrationInstanceWaitingForEvent.Should().NotBeNull();

        // Step 3a: Send first ExampleNotifyEvent event
        var expectedEventDataMessage = "The expected data message";
        await Fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceExampleNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstanceWaitingForEvent!.Id.ToString(),
                Data: new ExampleNotifyEventDataV1(expectedEventDataMessage)),
            CancellationToken.None);

        // Step 3b: Send another ExampleNotifyEvent event
        var ignoredEventDataMessage = "An incorrect data message";
        await Fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceExampleNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstanceWaitingForEvent.Id.ToString(),
                Data: new ExampleNotifyEventDataV1(ignoredEventDataMessage)),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (isTerminated, orchestrationInstance) = await Fixture.ProcessManagerClient
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
        orchestrationInstance.Should().NotBeNull();

        // Assert that custom status is the expected notify event data (and not the ignored notify event)
        orchestrationInstance!.Lifecycle.TerminationState.Should().Be(OrchestrationInstanceTerminationState.Succeeded);

        var expectedCustomState = JsonSerializer.Serialize(new WaitForNotifyEventStep.CustomState(expectedEventDataMessage));
        var ignoredCustomState = JsonSerializer.Serialize(new WaitForNotifyEventStep.CustomState(ignoredEventDataMessage));
        orchestrationInstance.Steps.Should()
            .HaveCount(1)
            .And.ContainSingle(s => s.CustomState == expectedCustomState)
            .And.NotContain(s => s.CustomState == ignoredCustomState);
    }
}
