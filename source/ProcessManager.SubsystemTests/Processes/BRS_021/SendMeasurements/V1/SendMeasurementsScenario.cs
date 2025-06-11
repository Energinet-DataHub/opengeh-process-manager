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
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.Core.TestCommon.Xunit.Attributes;
using Energinet.DataHub.Core.TestCommon.Xunit.Orderers;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.SendMeasurements;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
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
        var (success, instance) = await WaitForSendMeasurementsInstanceByIdempotencyKeyAsync(
                idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey);

        Assert.Multiple(
            () => Assert.True(
                success,
                $"An instance for idempotency key \"{_fixture.ScenarioState.Command.IdempotencyKey}\" should have been found"),
            () => Assert.NotNull(instance));

        _fixture.ScenarioState.Instance = instance;
    }

    [SubsystemFact]
    [ScenarioStep(4)]
    public async Task Then_BusinessValidationIsSuccessful()
    {
        Assert.NotNull(_fixture.ScenarioState.Instance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, instance) = await WaitForSendMeasurementsInstanceStepByIdempotencyKeyAsync(
                idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
                step: SendMeasurementsInstanceStep.BusinessValidation,
                stepMustBeSuccessful: true);

        _fixture.ScenarioState.Instance = instance;

        var hasValidationErrors = !string.IsNullOrEmpty(instance?.ValidationErrors);
        if (hasValidationErrors)
        {
            _fixture.Logger.WriteLine($"Business validation errors: {instance?.ValidationErrors}.");
            _fixture.ScenarioState.BusinessValidationFailed = true;
        }

        Assert.Multiple(
            () => Assert.True(success, $"Business validation step should be terminated."),
            () => Assert.NotNull(instance?.BusinessValidationSucceededAt),
            () => Assert.False(hasValidationErrors, $"Shouldn't have any validation errors. Validation errors: {instance?.ValidationErrors}"));
    }

    [SubsystemFact]
    [ScenarioStep(5)]
    public async Task AndThen_MeasurementsAreSentToMeasurementsSubsystem()
    {
        Assert.NotNull(_fixture.ScenarioState.Instance); // If orchestration instance wasn't found in earlier test, end test early.
        Assert.False(_fixture.ScenarioState.BusinessValidationFailed); // If business validation failed, end test early.

        var (success, instance) = await WaitForSendMeasurementsInstanceStepByIdempotencyKeyAsync(
            idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
            step: SendMeasurementsInstanceStep.ForwardToMeasurements);

        _fixture.ScenarioState.Instance = instance;

        Assert.Multiple(
            () => Assert.True(success, "Forward to measurements step should be running"),
            () => Assert.NotNull(instance?.SentToMeasurementsAt));
    }

    [SubsystemFact]
    [ScenarioStep(6)]
    public async Task AndThen_ReceivedSendMeasurementsNotifyFromMeasurementsTransitionsForwardToMeasurementsToSuccessful()
    {
        Assert.NotNull(_fixture.ScenarioState.Instance); // If orchestration instance wasn't found in earlier test, end test early.
        Assert.False(_fixture.ScenarioState.BusinessValidationFailed); // If business validation failed, end test early.

        // Simulate "ForwardMeteredDataNotifyV1" message from Measurements to Process Manager event hub
        var notifyFromMeasurementsMessage = new Brs021ForwardMeteredDataNotifyV1
        {
            Version = "1",
            OrchestrationInstanceId = _fixture.ScenarioState.Instance.Id.ToString(),
        };

        await _fixture.ProcessManagerEventHubProducerClient.SendAsync(
            [new EventData(notifyFromMeasurementsMessage.ToByteArray())],
            CancellationToken.None);

        var (success, instance) = await WaitForSendMeasurementsInstanceStepByIdempotencyKeyAsync(
            idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
            step: SendMeasurementsInstanceStep.ForwardToMeasurements,
            stepMustBeSuccessful: true);

        _fixture.ScenarioState.Instance = instance;

        Assert.Multiple(
            () => Assert.True(success, "Forward to Measurements step should be terminated"),
            () => Assert.NotNull(instance?.ReceivedFromMeasurementsAt));
    }

    [SubsystemFact]
    [ScenarioStep(7)]
    public async Task AndThen_EnqueueActorMessagesIsSentToEDI()
    {
        Assert.NotNull(_fixture.ScenarioState.Instance); // If orchestration instance wasn't found in earlier test, end test early.
        Assert.False(_fixture.ScenarioState.BusinessValidationFailed); // If business validation failed, end test early.

        var (success, instance) = await WaitForSendMeasurementsInstanceStepByIdempotencyKeyAsync(
            idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
            step: SendMeasurementsInstanceStep.EnqueueActorMessages);

        _fixture.ScenarioState.Instance = instance;

        Assert.Multiple(
            () => Assert.True(success, "Enqueue actor messages step should be running"),
            () => Assert.NotNull(instance?.SentToEnqueueActorMessagesAt));
    }

    [SubsystemFact]
    [ScenarioStep(8)]
    public async Task AndThen_ReceivingNotifyEnqueueActorMessagesCompletedTransitionsEnqueueActorMessagesStepToSuccessful()
    {
        Assert.NotNull(_fixture.ScenarioState.Instance); // If orchestration instance wasn't found in earlier test, end test early.
        Assert.False(_fixture.ScenarioState.BusinessValidationFailed); // If business validation failed, end test early.

        // Send notify "EnqueueActorMessagesCompleted" message to the orchestration instance
        await _fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new ForwardMeteredDataNotifyEventV1(
                OrchestrationInstanceId: _fixture.ScenarioState.Instance.Id.ToString()),
            CancellationToken.None);

        // Wait for the enqueue actor messages step to be terminated
        var (success, instance) = await WaitForSendMeasurementsInstanceStepByIdempotencyKeyAsync(
            idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
            step: SendMeasurementsInstanceStep.EnqueueActorMessages,
            stepMustBeSuccessful: true);

        _fixture.ScenarioState.Instance = instance;

        Assert.Multiple(
            () => Assert.True(success, "Enqueue actor messages step should be terminated"),
            () => Assert.NotNull(instance?.ReceivedFromEnqueueActorMessagesAt));
    }

    [SubsystemFact]
    [ScenarioStep(9)]
    public async Task AndThen_OrchestrationInstanceIsTerminatedWithSuccess()
    {
        Assert.NotNull(_fixture.ScenarioState.Instance); // If orchestration instance wasn't found in earlier test, end test early.
        Assert.False(_fixture.ScenarioState.BusinessValidationFailed); // If business validation failed, end test early.

        // Wait for the enqueue actor messages step to be terminated
        var (success, instance) = await WaitForSendMeasurementsInstanceByIdempotencyKeyAsync(
            idempotencyKey: _fixture.ScenarioState.Command.IdempotencyKey,
            instanceMustBeTerminated: true);

        _fixture.ScenarioState.Instance = instance;

        Assert.Multiple(
            () => Assert.True(success, "The instance should be terminated"),
            () => Assert.NotNull(instance?.TerminatedAt),
            () => Assert.Null(instance?.FailedAt));
    }

    /// <summary>
    /// Wait for a Send Measurements instance to be returned by the ProcessManager http client. If step inputs are provided,
    /// then the instance must have a step with the given state.
    /// </summary>
    /// <param name="idempotencyKey">Find an instance with the given idempotency key.</param>
    /// <param name="instanceMustBeTerminated">If true then the instance must be terminated.</param>
    /// <param name="timeoutInMinutes">How long to wait for the orchestration instance to be in the given state (defaults to 1).</param>
    private async Task<(
        bool Success,
        SendMeasurementsInstanceDto? SendMeasurementsInstance)> WaitForSendMeasurementsInstanceByIdempotencyKeyAsync(
            string idempotencyKey,
            bool instanceMustBeTerminated = false,
            int timeoutInMinutes = 1)
    {
        SendMeasurementsInstanceDto? instance = null;

        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                instance = await _fixture.ProcessManagerHttpClient
                    .GetSendMeasurementsInstanceByIdempotencyKeyAsync(
                        new GetSendMeasurementsInstanceByIdempotencyKeyQuery(
                            operatingIdentity: _fixture.EnergySupplierUserIdentity,
                            idempotencyKey: idempotencyKey),
                        CancellationToken.None);

                return instanceMustBeTerminated
                    ? instance is { TerminatedAt: not null }
                    : instance != null;
            },
            timeLimit: TimeSpan.FromMinutes(timeoutInMinutes),
            delay: TimeSpan.FromSeconds(1));

        return (success, instance);
    }

    /// <summary>
    /// Wait for a Send Measurements instance with the provided step state to be returned by
    /// the ProcessManager http client.
    /// </summary>
    /// <param name="idempotencyKey">Find an instance with the given idempotency key.</param>
    /// <param name="step">The step that must be running.</param>
    /// <param name="stepMustBeSuccessful">If true then the <paramref name="step"/> must be successful.</param>
    /// <param name="timeoutInMinutes">How long to wait for the orchestration instance to be in the given state (defaults to 1).</param>
    private async Task<(
        bool Success,
        SendMeasurementsInstanceDto? SendMeasurementsInstance)> WaitForSendMeasurementsInstanceStepByIdempotencyKeyAsync(
            string idempotencyKey,
            SendMeasurementsInstanceStep step,
            bool stepMustBeSuccessful = false,
            int timeoutInMinutes = 1)
    {
        SendMeasurementsInstanceDto? instance = null;

        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                instance = await _fixture.ProcessManagerHttpClient
                    .GetSendMeasurementsInstanceByIdempotencyKeyAsync(
                        new GetSendMeasurementsInstanceByIdempotencyKeyQuery(
                            operatingIdentity: _fixture.EnergySupplierUserIdentity,
                            idempotencyKey: idempotencyKey),
                        CancellationToken.None);

                if (instance == null)
                    return false;

                return stepMustBeSuccessful
                    ? step switch
                    {
                        SendMeasurementsInstanceStep.BusinessValidation => instance.BusinessValidationSucceededAt is not null,
                        SendMeasurementsInstanceStep.ForwardToMeasurements => instance.ReceivedFromMeasurementsAt is not null,
                        SendMeasurementsInstanceStep.EnqueueActorMessages => instance.ReceivedFromEnqueueActorMessagesAt is not null,
                        _ => throw new ArgumentOutOfRangeException(nameof(step), $"Unknown step: {step}."),
                    }
                    : step switch
                    {
                        SendMeasurementsInstanceStep.BusinessValidation => true,
                        SendMeasurementsInstanceStep.ForwardToMeasurements => instance.SentToMeasurementsAt is not null,
                        SendMeasurementsInstanceStep.EnqueueActorMessages => instance.SentToEnqueueActorMessagesAt is not null,
                        _ => throw new ArgumentOutOfRangeException(nameof(step), $"Unknown step: {step}."),
                    };
            },
            timeLimit: TimeSpan.FromMinutes(timeoutInMinutes),
            delay: TimeSpan.FromSeconds(1));

        return (success, instance);
    }
}
