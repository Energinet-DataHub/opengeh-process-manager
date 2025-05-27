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

using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_026_028.BRS_028.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// request calculated energy time series orchestration and monitor its status during its lifetime.
/// </summary>
[ParallelWorkflow(WorkflowBucket.Bucket02)]
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
    }

    public Task InitializeAsync()
    {
        _fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        _fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();
        _fixture.EnqueueBrs028ServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);
        _fixture.EnqueueBrs028ServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Tests the BRS-026 orchestration instance when the request is valid and actor messages should be enqueued.
    /// </summary>
    [Fact]
    public async Task Given_ValidRequestCalculatedWholesaleServices_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        const string gridAreaCode = "804";
        _fixture.OrchestrationsAppManager.MockServer.MockGetGridAreaOwner(gridAreaCode);

        // Step 1: Start new orchestration instance
        var requestCommand = GivenRequestCalculatedWholesaleServices(gridAreaCode);

        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            requestCommand,
            CancellationToken.None);

        // Step 2a: Query until waiting for EnqueueActorMessagesCompleted notify event
        var (isWaitingForNotify, orchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForStepToBeRunning<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: requestCommand.IdempotencyKey,
                stepSequence: EnqueueActorMessagesStep.StepSequence);

        isWaitingForNotify.Should()
            .BeTrue("because the orchestration instance should wait for a EnqueueActorMessagesCompleted notify event");

        // Step 2b: Verify an enqueue actor messages event is sent on the service bus
        var verifyEnqueueActorMessagesEvent = await _fixture.EnqueueBrs028ServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_028.Name, out var enqueueActorMessagesV1))
                        return false;

                    var requestAcceptedV1 = enqueueActorMessagesV1.ParseData<RequestCalculatedWholesaleServicesAcceptedV1>();
                    return requestAcceptedV1.OriginalTransactionId == requestCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue($"because a {nameof(RequestCalculatedWholesaleServicesAcceptedV1)} service bus message should have been sent");

        // Step 3: Send EnqueueActorMessagesCompleted event
        await _fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new RequestCalculatedWholesaleServicesNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstance!.Id.ToString()),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (orchestrationTerminatedWithSucceeded, terminatedOrchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: requestCommand.IdempotencyKey);

        orchestrationTerminatedWithSucceeded.Should().BeTrue(
            "because the orchestration instance should be terminated within given wait time");

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
    /// Tests the BRS-028 orchestration instance when the request is invalid and rejected actor messages should be enqueued.
    /// </summary>
    [Fact]
    public async Task Given_InvalidRequestCalculatedWholesaleServices_When_Started_Then_OrchestrationInstanceTerminatesWithFailed_AndThen_BusinessValidationStepFailed()
    {
        const string gridAreaCode = "804";
        _fixture.OrchestrationsAppManager.MockServer.MockGetGridAreaOwner(gridAreaCode);

        // Step 1: Start new orchestration instance
        var invalidRequestCommand = GivenRequestCalculatedWholesaleServices(gridAreaCode, shouldFailBusinessValidation: true);

        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            invalidRequestCommand,
            CancellationToken.None);

        // Step 2a: Query until waiting for EnqueueActorMessagesCompleted notify event
        var (isWaitingForNotify, orchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForStepToBeRunning<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: invalidRequestCommand.IdempotencyKey,
                stepSequence: EnqueueActorMessagesStep.StepSequence);

        isWaitingForNotify.Should()
            .BeTrue("because the orchestration instance should wait for a EnqueueActorMessagesCompleted notify event");

        // Step 2b: Verify an enqueue actor messages event is sent on the service bus
        var verifyEnqueueRejectedActorMessagesEvent = await _fixture.EnqueueBrs028ServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_028.Name, out var enqueueActorMessagesV1))
                        return false;

                    var requestAcceptedV1 = enqueueActorMessagesV1.ParseData<RequestCalculatedWholesaleServicesRejectedV1>();

                    requestAcceptedV1.ValidationErrors.Should()
                        .HaveCount(1)
                        .And.ContainSingle(
                            (e) => e.Message.Contains(
                                "Feltet EnergySupplier skal være udfyldt med et valid GLN/EIC nummer når en elleverandør anmoder om data"));
                    return requestAcceptedV1.OriginalTransactionId == invalidRequestCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueRejectedActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue($"because a {nameof(RequestCalculatedWholesaleServicesInputV1)} service bus message should have been sent");

        // Step 3: Send EnqueueActorMessagesCompleted event
        await _fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new RequestCalculatedWholesaleServicesNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstance!.Id.ToString()),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (orchestrationTerminatedWithSucceeded, terminatedOrchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: invalidRequestCommand.IdempotencyKey);

        orchestrationTerminatedWithSucceeded.Should().BeTrue(
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

    private RequestCalculatedWholesaleServicesCommandV1 GivenRequestCalculatedWholesaleServices(
        string gridAreaCode,
        bool shouldFailBusinessValidation = false)
    {
        const string energySupplierNumber = "1111111111111";
        var energySupplierRole = ActorRole.EnergySupplier.Name;

        return new RequestCalculatedWholesaleServicesCommandV1(
            _fixture.DefaultActorIdentity,
            new RequestCalculatedWholesaleServicesInputV1(
                ActorMessageId: Guid.NewGuid().ToString(),
                TransactionId: Guid.NewGuid().ToString(),
                RequestedForActorNumber: energySupplierNumber,
                RequestedForActorRole: energySupplierRole,
                RequestedByActorNumber: energySupplierNumber,
                RequestedByActorRole: energySupplierRole,
                BusinessReason: BusinessReason.WholesaleFixing.Name,
                PeriodStart: "2024-12-31T23:00:00Z",
                PeriodEnd: "2025-01-31T23:00:00Z",
                Resolution: null,
                // EnergySupplierNumber is required when RequestedByActorRole is EnergySupplier, so the request will fail if not provided.
                EnergySupplierNumber: !shouldFailBusinessValidation ? energySupplierNumber : null,
                ChargeOwnerNumber: null,
                GridAreas: [gridAreaCode],
                SettlementVersion: null,
                ChargeTypes: null),
            idempotencyKey: Guid.NewGuid().ToString());
    }
}
