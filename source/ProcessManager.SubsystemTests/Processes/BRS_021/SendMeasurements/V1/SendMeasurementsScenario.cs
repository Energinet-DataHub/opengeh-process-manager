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

using Azure.Messaging.EventHubs;
using Energinet.DataHub.Core.TestCommon.Xunit.Attributes;
using Energinet.DataHub.Core.TestCommon.Xunit.Orderers;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures.Extensions;
using Google.Protobuf;
using NodaTime;
using NodaTime.Text;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_021.SendMeasurements.V1;

[TestCaseOrderer(
    ordererTypeName: TestCaseOrdererLocation.OrdererTypeName,
    ordererAssemblyName: TestCaseOrdererLocation.OrdererAssemblyName)]
public class SendMeasurementsScenario
    : IClassFixture<ProcessManagerFixture<SendMeasurementsScenarioState>>,
        IAsyncLifetime
{
    private readonly ProcessManagerFixture<SendMeasurementsScenarioState> _fixture;

    public SendMeasurementsScenario(
        ProcessManagerFixture<SendMeasurementsScenarioState> fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _fixture.SetTestOutputHelper(null);
        return Task.CompletedTask;
    }

    [SubsystemFact]
    [ScenarioStep(1)]
    public void Given_ValidSendMeasurementsCommand()
    {
        var start = Instant.FromUtc(2025, 05, 21, 23, 00, 00);
        var end = start.Plus(Duration.FromDays(1));
        var periodDuration = end - start;
        var resolution = Resolution.QuarterHourly;
        var numberOfMeasurements = (int)(periodDuration.TotalMinutes / 15); // Resolution = QuarterHourly = 15 minutes

        var actorMessageId = Guid.NewGuid().ToTestMessageUuid();
        var transactionId = Guid.NewGuid().ToTestMessageUuid();
        _fixture.ScenarioState = new SendMeasurementsScenarioState(
            Command: new ForwardMeteredDataCommandV1(
                operatingIdentity: _fixture.GridAccessProviderActorIdentity,
                inputParameter: new ForwardMeteredDataInputV1(
                    ActorMessageId: actorMessageId,
                    TransactionId: transactionId,
                    ActorNumber: _fixture.GridAccessProviderActorIdentity.ActorNumber.Value,
                    ActorRole: _fixture.GridAccessProviderActorIdentity.ActorRole.Name,
                    BusinessReason: BusinessReason.PeriodicMetering.Name,
                    MeteringPointId: "123456789012345678".ToTestMeteringPointId(),
                    MeteringPointType: MeteringPointType.Consumption.Name,
                    ProductNumber: null,
                    MeasureUnit: MeasurementUnit.KilowattHour.Name,
                    RegistrationDateTime: InstantPattern.General.Format(end),
                    Resolution: resolution.Name,
                    StartDateTime: InstantPattern.General.Format(start),
                    EndDateTime: InstantPattern.General.Format(end),
                    GridAccessProviderNumber: _fixture.GridAccessProviderActorIdentity.ActorNumber.Value,
                    MeteredDataList: Enumerable.Range(0, numberOfMeasurements)
                        .Select(
                            i => new ForwardMeteredDataInputV1.MeteredData(
                                Position: (i + 1).ToString(), // Position is 1 based
                                EnergyQuantity: "42.0",
                                QuantityQuality: Quality.AsProvided.Name))
                        .ToList()),
                idempotencyKey: transactionId));
    }

    [SubsystemFact]
    [ScenarioStep(2)]
    public async Task AndGiven_StartNewOrchestrationInstanceIsSent()
    {
        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            _fixture.ScenarioState.Command,
            CancellationToken.None);
    }

    [SubsystemFact]
    [ScenarioStep(3)]
    public async Task When_OrchestrationInstanceIsRunning()
    {
        var (success, orchestrationInstance, _) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                ForwardMeteredDataInputV1, SendMeasurementsScenarioState>(
                _fixture.ScenarioState.Command.IdempotencyKey,
                OrchestrationInstanceLifecycleState.Running);

        Assert.Multiple(
            () => Assert.True(
                success,
                $"An orchestration instance for idempotency key \"{_fixture.ScenarioState.Command.IdempotencyKey}\" should have been found"),
            () => Assert.NotNull(orchestrationInstance));

        _fixture.ScenarioState.OrchestrationInstance = orchestrationInstance;
    }

    [SubsystemFact]
    [ScenarioStep(4)]
    public async Task Then_BusinessValidationIsSuccessful()
    {
        Assert.NotNull(_fixture.ScenarioState.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, businessValidationStep) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                ForwardMeteredDataInputV1, SendMeasurementsScenarioState>(
                idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.OrchestrationDescriptionBuilder.BusinessValidationStep,
                stepState: StepInstanceLifecycleState.Terminated);

        _fixture.ScenarioState.OrchestrationInstance = orchestrationInstance;

        if (businessValidationStep?.CustomState.Length > 0)
            _fixture.Logger.WriteLine($"Business validation step custom state: {businessValidationStep?.CustomState}.");

        Assert.Multiple(
            () => Assert.True(success, $"Business validation step should be terminated."),
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, businessValidationStep?.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, businessValidationStep?.Lifecycle.TerminationState));
    }

    [SubsystemFact]
    [ScenarioStep(5)]
    public async Task AndThen_MeasurementsAreSentToMeasurementsSubsystem()
    {
        Assert.NotNull(_fixture.ScenarioState.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, forwardToMeasurementsStep) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                ForwardMeteredDataInputV1, SendMeasurementsScenarioState>(
                idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.OrchestrationDescriptionBuilder.ForwardToMeasurementsStep,
                stepState: StepInstanceLifecycleState.Running);

        _fixture.ScenarioState.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Forward to measurements step should be running"),
            () => Assert.Equal(StepInstanceLifecycleState.Running, forwardToMeasurementsStep?.Lifecycle.State),
            () => Assert.Null(forwardToMeasurementsStep?.Lifecycle.TerminationState));
    }

    [SubsystemFact]
    [ScenarioStep(6)]
    public async Task AndThen_ReceivedSendMeasurementsNotifyFromMeasurementsTransitionsForwardToMeasurementsToSuccessful()
    {
        Assert.NotNull(_fixture.ScenarioState.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        // Simulate "ForwardMeteredDataNotifyV1" message from Measurements to Process Manager event hub
        var notifyFromMeasurementsMessage = new Brs021ForwardMeteredDataNotifyV1
        {
            Version = "1",
            OrchestrationInstanceId = _fixture.ScenarioState.OrchestrationInstance.Id.ToString(),
        };

        await _fixture.ProcessManagerEventHubProducerClient.SendAsync(
            [new EventData(notifyFromMeasurementsMessage.ToByteArray())],
            CancellationToken.None);

        var (success, orchestrationInstance, forwardToMeasurementsStep) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                ForwardMeteredDataInputV1, SendMeasurementsScenarioState>(
                idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.OrchestrationDescriptionBuilder.ForwardToMeasurementsStep,
                stepState: StepInstanceLifecycleState.Terminated);

        _fixture.ScenarioState.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Forward to Measurements step should be terminated"),
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, forwardToMeasurementsStep?.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, forwardToMeasurementsStep?.Lifecycle.TerminationState));
    }

    [SubsystemFact]
    [ScenarioStep(7)]
    public async Task AndThen_EnqueueActorMessagesIsSentToEDI()
    {
        Assert.NotNull(_fixture.ScenarioState.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, enqueueActorMessagesStep) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                ForwardMeteredDataInputV1, SendMeasurementsScenarioState>(
                idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.OrchestrationDescriptionBuilder.EnqueueActorMessagesStep,
                stepState: StepInstanceLifecycleState.Running);

        _fixture.ScenarioState.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Enqueue actor messages step should be running"),
            () => Assert.Equal(StepInstanceLifecycleState.Running, enqueueActorMessagesStep?.Lifecycle.State),
            () => Assert.Null(enqueueActorMessagesStep?.Lifecycle.TerminationState));
    }

    [SubsystemFact]
    [ScenarioStep(8)]
    public async Task AndThen_ReceivingNotifyEnqueueActorMessagesCompletedTransitionsEnqueueActorMessagesStepToSuccessful()
    {
        Assert.NotNull(_fixture.ScenarioState.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        // Send notify "EnqueueActorMessagesCompleted" message to the orchestration instance
        await _fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new ForwardMeteredDataNotifyEventV1(
                OrchestrationInstanceId: _fixture.ScenarioState.OrchestrationInstance.Id.ToString()),
            CancellationToken.None);

        // Wait for the enqueue actor messages step to be terminated
        var (success, orchestrationInstance, enqueueActorMessagesStep) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                ForwardMeteredDataInputV1, SendMeasurementsScenarioState>(
                idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.OrchestrationDescriptionBuilder.EnqueueActorMessagesStep,
                stepState: StepInstanceLifecycleState.Terminated);

        _fixture.ScenarioState.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Enqueue actor messages step should be terminated"),
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, enqueueActorMessagesStep?.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, enqueueActorMessagesStep?.Lifecycle.TerminationState));
    }

    [SubsystemFact]
    [ScenarioStep(9)]
    public async Task AndThen_OrchestrationInstanceIsTerminatedWithSuccess()
    {
        Assert.NotNull(_fixture.ScenarioState.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, _) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                ForwardMeteredDataInputV1, SendMeasurementsScenarioState>(
                idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
                orchestrationInstanceState: OrchestrationInstanceLifecycleState.Terminated);

        _fixture.ScenarioState.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "The orchestration instance should be terminated"),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, orchestrationInstance?.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, orchestrationInstance?.Lifecycle.TerminationState));
    }
}
