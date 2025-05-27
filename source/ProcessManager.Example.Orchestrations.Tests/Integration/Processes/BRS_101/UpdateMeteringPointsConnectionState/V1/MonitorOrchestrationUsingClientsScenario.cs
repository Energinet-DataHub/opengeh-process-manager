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

using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_101.UpdateMeteringPointConnectionState;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_101.UpdateMeteringPointsConnectionState.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to
/// start/notify a BRS-101 Update MeteringPoint Connection State orchestration instance
/// and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private readonly ActorIdentityDto _actorIdentity = new(
        ActorNumber: ActorNumber.Create("1234567891234"),
        ActorRole: ActorRole.EnergySupplier);

    public MonitorOrchestrationUsingClientsScenario(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services
            .AddTokenCredentialProvider()
            .AddInMemoryConfiguration(new Dictionary<string, string?>
            {
                // Process Manager HTTP client
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                    = SubsystemAuthenticationOptionsForTests.ApplicationIdUri,
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                    = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                    = Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),

                // Process Manager message client
                [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.StartTopicName)}"]
                    = Fixture.ExampleOrchestrationsAppManager.ProcessManagerStartTopic.Name,
                [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.NotifyTopicName)}"]
                    = Fixture.ProcessManagerAppManager.ProcessManagerNotifyTopic.Name,
                [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataStartTopicName)}"]
                    = Fixture.ExampleOrchestrationsAppManager.Brs021ForwardMeteredDataStartTopic.Name,
                [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataNotifyTopicName)}"]
                    = Fixture.ExampleOrchestrationsAppManager.Brs021ForwardMeteredDataNotifyTopic.Name,
            });

        // Process Manager HTTP client
        services.AddProcessManagerHttpClients();

        // Process Manager message client
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(Fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerMessageClient();

        ServiceProvider = services.BuildServiceProvider();

        ProcessManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
        ProcessManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    private IProcessManagerClient ProcessManagerClient { get; }

    private IProcessManagerMessageClient ProcessManagerMessageClient { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        Fixture.EnqueueBrs101ServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.ExampleOrchestrationsAppManager.SetTestOutputHelper(null!);

        Fixture.EnqueueBrs101ServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Tests the BRS-101 Update MeteringPoint Connection State orchestration instance
    /// when the request is valid and actor messages should be enqueued.
    /// </summary>
    [Fact]
    public async Task Given_ValidRequestToUpdateMeteringPointConnectionState_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        // Step 1: Act as EDI => Send start command to start new orchestration instance
        var startCommand = GivenStartCommand();

        await ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startCommand,
            CancellationToken.None);

        // Step 2: Wait for enqueue actor messages request is sent to EDI
        string? orchestrationInstanceId = null;
        var verifyEnqueueActorMessagesEvent = await Fixture.EnqueueBrs101ServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_101_UpdateMeteringPointConnectionState.Name, out var enqueueActorMessagesV1))
                        return false;

                    orchestrationInstanceId = enqueueActorMessagesV1.OrchestrationInstanceId;
                    var requestAcceptedV1 = enqueueActorMessagesV1.ParseData<UpdateMeteringPointConnectionStateAcceptedV1>();
                    return requestAcceptedV1.OriginalTransactionId == startCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue(
            $"because a {nameof(UpdateMeteringPointConnectionStateAcceptedV1)} service bus message should have been sent");

        // Step 3: Act as EDI => Send "notify" event to orchestration instance, to inform that messages has been enqueued
        await ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new UpdateMeteringPointConnectionStateNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstanceId!),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (orchestrationTerminated, terminatedOrchestrationInstance) = await ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<UpdateMeteringPointConnectionStateInputV1>(
                startCommand.IdempotencyKey);

        orchestrationTerminated.Should().BeTrue(
            "because the orchestration instance should be terminated within the given wait time");

        // Orchestration instance and all steps should be Succeeded
        using var assertionScope = new AssertionScope();
        terminatedOrchestrationInstance!.Lifecycle.TerminationState.Should()
            .NotBeNull()
            .And.Be(OrchestrationInstanceTerminationState.Succeeded);

        terminatedOrchestrationInstance.Steps.Should()
            .AllSatisfy(
                s =>
                {
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Succeeded);
                });
    }

    /// <summary>
    /// Tests the BRS-101 Update MeteringPoint Connection State orchestration instance
    /// when the request is invalid and rejected actor messages should be enqueued.
    /// </summary>
    [Fact]
    public async Task Given_InvalidRequestToUpdateMeteringPointConnectionState_When_Started_Then_OrchestrationInstanceTerminatesWithFailed_AndThen_BusinessValidationStepFailed()
    {
        // Step 1: Act as EDI => Send start command to start new orchestration instance
        var startCommand = GivenStartCommand(shouldFailBusinessValidation: true);

        await ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startCommand,
            CancellationToken.None);

        // Step 2: Wait for enqueue actor messages request is sent to EDI
        string? orchestrationInstanceId = null;
        var verifyEnqueueActorMessagesEvent = await Fixture.EnqueueBrs101ServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_101_UpdateMeteringPointConnectionState.Name, out var enqueueActorMessagesV1))
                        return false;

                    orchestrationInstanceId = enqueueActorMessagesV1.OrchestrationInstanceId;
                    var requestRejectedV1 = enqueueActorMessagesV1.ParseData<UpdateMeteringPointConnectionStateRejectedV1>();

                    requestRejectedV1.ValidationErrors.Should()
                        .HaveCount(1)
                        .And.ContainSingle(
                            (e) => e.Message.Contains(
                                "MeteringPointId skal være udfyldt / MeteringPointId must have a value"));

                    return requestRejectedV1.OriginalTransactionId == startCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue(
            $"because a {nameof(UpdateMeteringPointConnectionStateRejectedV1)} service bus message should have been sent");

        // Step 3: Act as EDI => Send "notify" event to orchestration instance, to inform that messages has been enqueued
        await ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new UpdateMeteringPointConnectionStateNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstanceId!),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (orchestrationTerminated, terminatedOrchestrationInstance) = await ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<UpdateMeteringPointConnectionStateInputV1>(
                startCommand.IdempotencyKey);

        orchestrationTerminated.Should().BeTrue(
            "because the orchestration instance should be terminated within the given wait time");

        // Orchestration instance and validation steps should be Failed
        using var assertionScope = new AssertionScope();
        terminatedOrchestrationInstance!.Lifecycle.TerminationState.Should()
            .NotBeNull()
            .And.Be(OrchestrationInstanceTerminationState.Failed);

        terminatedOrchestrationInstance.Steps.OrderBy(s => s.Sequence).Should()
            .SatisfyRespectively(
                s =>
                {
                    // Validation step should be failed
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Failed);
                },
                s =>
                {
                    // Enqueue rejected messages step should be succeeded
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Succeeded);
                });
    }

    private StartUpdateMeteringPointConnectionStateCommandV1 GivenStartCommand(
        bool shouldFailBusinessValidation = false)
    {
        return new StartUpdateMeteringPointConnectionStateCommandV1(
            _actorIdentity,
            new UpdateMeteringPointConnectionStateInputV1(
                RequestedByActorNumber: _actorIdentity.ActorNumber.Value,
                RequestedByActorRole: _actorIdentity.ActorRole.Name,
                ActorMessageId: Guid.NewGuid().ToString(),
                TransactionId: Guid.NewGuid().ToString(),
                // MeteringPointId is required, so the request will fail if not provided.
                MeteringPointId: !shouldFailBusinessValidation ? "TODO: Valid MeteringPoint ID" : string.Empty,
                IsConnected: true),
            idempotencyKey: Guid.NewGuid().ToString());
    }
}
