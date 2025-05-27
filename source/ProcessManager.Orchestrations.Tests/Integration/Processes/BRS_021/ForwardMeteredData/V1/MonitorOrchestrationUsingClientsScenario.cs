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
using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.Core.FunctionApp.TestCommon.EventHub.ListenerMock;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ElectricityMarket.Integration.Models.Common;
using Energinet.DataHub.ElectricityMarket.Integration.Models.ProcessDelegation;
using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Triggers;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Google.Protobuf;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit.Abstractions;

using ElectricityMarketModels = Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;
using MeteringPointType = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.MeteringPointType;
using OrchestrationInstanceTerminationState = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance.OrchestrationInstanceTerminationState;
using Quality = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Quality;
using Resolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;
using StepInstanceLifecycleState = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance.StepInstanceLifecycleState;
using StepInstanceTerminationState = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance.StepInstanceTerminationState;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// forward metered data flow
/// </summary>
[ParallelWorkflow(WorkflowBucket.Bucket03)]
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private const string ProcessManagerEventHubProducerClientName = "ProcessManagerEventHubProducerClient";
    private const string MeteringPointId = "571313101700011887";
    private const string EnergySupplier = "1111111111111";
    private const string GridAccessProvider = "2222222222222";
    private const string DelegatedToGridAccessProvider = "9999999999999";
    private const string NeighborGridAreaOwner1 = "3333333333331";
    private const string NeighborGridAreaOwner2 = "3333333333332";
    private const string GridArea = "804";
    private static readonly Instant _validFrom = Instant.FromUtc(2024, 11, 30, 23, 00, 00);
    private static readonly Instant _validTo = Instant.FromUtc(2024, 12, 31, 23, 00, 00);

    private readonly OrchestrationsAppFixture _fixture;

    public MonitorOrchestrationUsingClientsScenario(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);

        // Add event hub producer client for ProcessManagerEventHub to simulate the notification event from measurements
        var services = new ServiceCollection();
        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<EventHubProducerClient, EventHubProducerClientOptions>(
                        (_, _, _) =>
                        {
                            return new EventHubProducerClient(
                                _fixture.IntegrationTestConfiguration.EventHubFullyQualifiedNamespace,
                                _fixture.OrchestrationsAppManager.ProcessManagerEventhubName,
                                _fixture.IntegrationTestConfiguration.Credential);
                        })
                    .WithName(ProcessManagerEventHubProducerClientName);
            });
        ServiceProvider = services.BuildServiceProvider();
        var eventHubClientFactory = ServiceProvider.GetRequiredService<IAzureClientFactory<EventHubProducerClient>>();
        ProcessManagerEventHubProducerClient = eventHubClientFactory.CreateClient(ProcessManagerEventHubProducerClientName);
    }

    private ServiceProvider ServiceProvider { get; }

    private EventHubProducerClient ProcessManagerEventHubProducerClient { get; }

    public Task InitializeAsync()
    {
        _fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        _fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();
        _fixture.EnqueueBrs021ForwardMeteredDataServiceBusListener.ResetMessageHandlersAndReceivedMessages();
        _fixture.EventHubListener.Reset();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task
        Given_ValidForwardMeteredDataInputV1_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        // Arrange
        SetupElectricityMarketWireMocking();

        var input = CreateForwardMeteredDataInputV1();

        var forwardCommand = new ForwardMeteredDataCommandV1(
            new ActorIdentityDto(ActorNumber.Create(input.ActorNumber), ActorRole.GridAccessProvider),
            input,
            idempotencyKey: Guid.NewGuid().ToString());

        // Act
        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(forwardCommand, CancellationToken.None);

        // Step 2a: Query until waiting for Event Hub notify event from Measurements
        var (isWaitingForMeasurementsNotify, orchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForStepToBeRunning<ForwardMeteredDataInputV1>(
                forwardCommand.IdempotencyKey,
                OrchestrationDescriptionBuilder.ForwardToMeasurementsStep);

        isWaitingForMeasurementsNotify.Should()
            .BeTrue("because the orchestration instance should wait for a notify event from Measurements");

        // Verify that an persistSubmittedTransaction event is sent on the event hub
        var verifyForwardMeteredDataToMeasurementsEvent = await _fixture.EventHubListener.When(
                (message) =>
                {
                    var persistSubmittedTransaction = PersistSubmittedTransaction.Parser.ParseFrom(message.EventBody.ToArray());

                    var orchestrationIdMatches = persistSubmittedTransaction.OrchestrationInstanceId == orchestrationInstance!.Id.ToString();
                    var transactionIdMatches = persistSubmittedTransaction.TransactionId == input.TransactionId;

                    return orchestrationIdMatches && transactionIdMatches;
                })
            .VerifyCountAsync(1);

        var persistSubmittedTransactionEventFound = verifyForwardMeteredDataToMeasurementsEvent.Wait(TimeSpan.FromSeconds(60));
        persistSubmittedTransactionEventFound.Should().BeTrue($"because a {nameof(PersistSubmittedTransaction)} event should have been sent");

        // Send a notification to the Process Manager Event Hub to simulate the notification event from measurements
        var notifyFromMeasurements = new Brs021ForwardMeteredDataNotifyV1()
        {
            Version = "v1", // Measurements sends "v1" instead of "1" as version
            OrchestrationInstanceId = orchestrationInstance!.Id.ToString(),
        };

        var eventHubEventData = new EventData(notifyFromMeasurements.ToByteArray());
        await ProcessManagerEventHubProducerClient.SendAsync([eventHubEventData], CancellationToken.None);

        // Wait for enqueue messages sent to EDI and send mock notify response to Process Manager
        await _fixture.EnqueueBrs021ForwardMeteredDataServiceBusListener.WaitOnEnqueueMessagesInEdiAndMockNotifyToProcessManager(
            processManagerMessageClient: _fixture.ProcessManagerMessageClient,
            orchestrationInstanceId: orchestrationInstance.Id,
            messageId: forwardCommand.ActorMessageId);

        // Query until terminated
        var (orchestrationTerminatedWithSucceeded, terminatedOrchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<ForwardMeteredDataInputV1>(
                idempotencyKey: forwardCommand.IdempotencyKey);

        orchestrationTerminatedWithSucceeded.Should().BeTrue(
            "because the orchestration instance should be terminated within given wait time");

        // Orchestration instance and all steps should be Succeeded
        using var assertionScope = new AssertionScope();
        terminatedOrchestrationInstance!.Lifecycle.TerminationState.Should()
            .NotBeNull()
            .And.Be(OrchestrationInstanceTerminationState.Succeeded);

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(MeteringPointId),
            ValidFrom: _validFrom.ToDateTimeOffset(),
            ValidTo: _validTo.ToDateTimeOffset(),
            CurrentGridAreaCode: new GridAreaCode(GridArea),
            CurrentGridAccessProvider: ActorNumber.Create(GridAccessProvider),
            CurrentNeighborGridAreaOwners: [NeighborGridAreaOwner1, NeighborGridAreaOwner2],
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.Production,
            MeteringPointSubType: MeteringPointSubType.Physical,
            Resolution: Resolution.Hourly,
            MeasurementUnit: MeasurementUnit.KilowattHour,
            ProductId: "Tariff",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create(EnergySupplier));
        var expectedCustomStateV1 = new ForwardMeteredDataCustomStateV2(
            HistoricalMeteringPointMasterData:
            [
                ForwardMeteredDataCustomStateV2.MasterData.FromMeteringPointMasterData(meteringPointMasterData)
            ]);

        terminatedOrchestrationInstance.CustomState.Should()
            .BeEquivalentTo(JsonSerializer.Serialize(expectedCustomStateV1));

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

    [Fact]
    public async Task
        Given_ValidForwardMeteredDataInputV1FromDelegatedGridOperator_When_StartedAndDelegation_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        // Arrange
        SetupElectricityMarketWireMocking();
        SetupElectricityMarketDelegationWireMocking();

        var input = CreateForwardMeteredDataInputV1();

        var forwardCommand = new ForwardMeteredDataCommandV1(
            new ActorIdentityDto(ActorNumber.Create(DelegatedToGridAccessProvider), ActorRole.Delegated),
            input,
            idempotencyKey: Guid.NewGuid().ToString());

        // Act
        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(forwardCommand, CancellationToken.None);

        // Step 2a: Query until waiting for Event Hub notify event from Measurements
        var (isWaitingForMeasurementsNotify, orchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForStepToBeRunning<ForwardMeteredDataInputV1>(
                forwardCommand.IdempotencyKey,
                OrchestrationDescriptionBuilder.ForwardToMeasurementsStep);

        isWaitingForMeasurementsNotify.Should()
            .BeTrue("because the orchestration instance should wait for a notify event from Measurements");

        // Verify that an persistSubmittedTransaction event is sent on the event hub
        var verifyForwardMeteredDataToMeasurementsEvent = await _fixture.EventHubListener.When(
                (message) =>
                {
                    var persistSubmittedTransaction = PersistSubmittedTransaction.Parser.ParseFrom(message.EventBody.ToArray());

                    var orchestrationIdMatches = persistSubmittedTransaction.OrchestrationInstanceId == orchestrationInstance!.Id.ToString();
                    var transactionIdMatches = persistSubmittedTransaction.TransactionId == input.TransactionId;

                    return orchestrationIdMatches && transactionIdMatches;
                })
            .VerifyCountAsync(1);

        var persistSubmittedTransactionEventFound = verifyForwardMeteredDataToMeasurementsEvent.Wait(TimeSpan.FromSeconds(60));
        persistSubmittedTransactionEventFound.Should().BeTrue($"because a {nameof(PersistSubmittedTransaction)} event should have been sent");

        // Send a notification to the Process Manager Event Hub to simulate the notification event from measurements
        var notifyFromMeasurements = new Brs021ForwardMeteredDataNotifyV1()
        {
            Version = "v1", // Measurements sends "v1" instead of "1" as version
            OrchestrationInstanceId = orchestrationInstance!.Id.ToString(),
        };

        var eventHubEventData = new EventData(notifyFromMeasurements.ToByteArray());
        await ProcessManagerEventHubProducerClient.SendAsync([eventHubEventData], CancellationToken.None);

        // Wait for enqueue messages sent to EDI and send mock notify response to Process Manager
        await _fixture.EnqueueBrs021ForwardMeteredDataServiceBusListener.WaitOnEnqueueMessagesInEdiAndMockNotifyToProcessManager(
            processManagerMessageClient: _fixture.ProcessManagerMessageClient,
            orchestrationInstanceId: orchestrationInstance.Id,
            messageId: forwardCommand.ActorMessageId);

        // Query until terminated
        var (orchestrationTerminatedWithSucceeded, terminatedOrchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<ForwardMeteredDataInputV1>(
                idempotencyKey: forwardCommand.IdempotencyKey);

        orchestrationTerminatedWithSucceeded.Should().BeTrue(
            "because the orchestration instance should be terminated within given wait time");

        // Orchestration instance and all steps should be Succeeded
        using var assertionScope = new AssertionScope();
        terminatedOrchestrationInstance!.Lifecycle.TerminationState.Should()
            .NotBeNull()
            .And.Be(OrchestrationInstanceTerminationState.Succeeded);

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(MeteringPointId),
            ValidFrom: _validFrom.ToDateTimeOffset(),
            ValidTo: _validTo.ToDateTimeOffset(),
            CurrentGridAreaCode: new GridAreaCode(GridArea),
            CurrentGridAccessProvider: ActorNumber.Create(GridAccessProvider),
            CurrentNeighborGridAreaOwners: [NeighborGridAreaOwner1, NeighborGridAreaOwner2],
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.Production,
            MeteringPointSubType: MeteringPointSubType.Physical,
            Resolution: Resolution.Hourly,
            MeasurementUnit: MeasurementUnit.KilowattHour,
            ProductId: "Tariff",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create(EnergySupplier));
        var expectedCustomStateV1 = new ForwardMeteredDataCustomStateV2(
            HistoricalMeteringPointMasterData:
            [
                ForwardMeteredDataCustomStateV2.MasterData.FromMeteringPointMasterData(meteringPointMasterData),
            ]);

        terminatedOrchestrationInstance.CustomState.Should()
            .BeEquivalentTo(JsonSerializer.Serialize(expectedCustomStateV1));

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

    [Fact]
    public async Task Given_InvalidForwardMeteredDataInputV1_When_Started_Then_OrchestrationInstanceTerminatesWithFailed_AndThen_BusinessValidationStepFailed()
    {
        // Given
        SetupElectricityMarketWireMocking();

        var invalidInput = CreateForwardMeteredDataInputV1() with { EndDateTime = null };

        var invalidForwardCommand = new ForwardMeteredDataCommandV1(
            new ActorIdentityDto(ActorNumber.Create(invalidInput.ActorNumber), ActorRole.GridAccessProvider),
            invalidInput,
            idempotencyKey: Guid.NewGuid().ToString());

        // When
        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(invalidForwardCommand, CancellationToken.None);

        // Then
        // Query until waiting for EnqueueActorMessagesCompleted notify event (a reject message should be enqueued)
        var (isWaitingForNotify, orchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForStepToBeRunning<ForwardMeteredDataInputV1>(
                invalidForwardCommand.IdempotencyKey,
                OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);

        isWaitingForNotify.Should()
            .BeTrue("because the orchestration instance should wait for a EnqueueActorMessagesCompleted notify event");

        // Verify an enqueue actor messages event is sent on the service bus
        var verifyEnqueueRejectedActorMessagesEvent = await _fixture.EnqueueBrs021ForwardMeteredDataServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_021_ForwardedMeteredData.Name, out var enqueueActorMessagesV1))
                        return false;

                    var forwardMeteredDataRejectedV1 = enqueueActorMessagesV1.ParseData<ForwardMeteredDataRejectedV1>();

                    forwardMeteredDataRejectedV1.ValidationErrors.Should()
                        .HaveCount(2)
                        .And.Contain((e) => e.Message.Equals(PeriodValidationRule.InvalidEndDate.Message))
                        .And.Contain((e) => e.Message.Equals(MeteringPointValidationRule.MeteringPointDoesntExistsError[0].Message));
                    forwardMeteredDataRejectedV1.MeteringPointId.Should()
                        .Be(MeteringPointId);
                    return forwardMeteredDataRejectedV1.OriginalTransactionId == invalidForwardCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueRejectedActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue($"because a {nameof(ForwardMeteredDataRejectedV1)} service bus message should have been sent");

        // Send EnqueueActorMessagesCompleted event
        await _fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new ForwardMeteredDataNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstance!.Id.ToString()),
            CancellationToken.None);

        // Query until terminated
        var (orchestrationTerminatedWithSucceeded, terminatedOrchestrationInstance) = await _fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<ForwardMeteredDataInputV1>(
                idempotencyKey: invalidForwardCommand.IdempotencyKey);

        orchestrationTerminatedWithSucceeded.Should().BeTrue(
            "because the orchestration instance should be terminated within given wait time");

        // Orchestration instance and validation steps should be Failed
        using var assertionScope = new AssertionScope();
        terminatedOrchestrationInstance!.Lifecycle.TerminationState.Should()
            .NotBeNull()
            .And.Be(OrchestrationInstanceTerminationState.Failed);

        terminatedOrchestrationInstance.CustomState.Should()
            .BeEquivalentTo(JsonSerializer.Serialize(new ForwardMeteredDataCustomStateV2([])));

        terminatedOrchestrationInstance.Steps.OrderBy(s => s.Sequence)
            .Should()
            .SatisfyRespectively(
                s =>
                {
                    // Validation step should be failed
                    s.Sequence.Should().Be(OrchestrationDescriptionBuilder.BusinessValidationStep);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Failed);
                },
                s =>
                {
                    // Forward to measurements step should be skipped
                    s.Sequence.Should().Be(OrchestrationDescriptionBuilder.ForwardToMeasurementsStep);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Skipped);
                },
                s =>
                {
                    // Find receiver step should be skipped
                    s.Sequence.Should().Be(OrchestrationDescriptionBuilder.FindReceiversStep);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Skipped);
                },
                s =>
                {
                    // Enqueue actor messages step should be succeeded
                    s.Sequence.Should().Be(OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Succeeded);
                });
    }

    /// <summary>
    /// With this test we verify the function will be retried and at least executed more than once,
    /// if we send an invalid notify event.
    /// The reason for only verifying that the function is executed twice is to save time in the test.
    /// And also we shouldn't have to test the attribute ExponentialBackoffRetry, since it's an
    /// out-of-box functionality and we expect it to work.
    /// </summary>
    [Fact]
    public async Task Given_InvalidNotifyEvent_When_NotifyOrchestrationInstance_Then_EnqueueMeteredDataTriggerIsExecutedAtLeastTwice()
    {
        // Given
        var invalidNotifyFromMeasurements = new Brs021ForwardMeteredDataNotifyV1()
        {
            Version = "invalid-value",
            OrchestrationInstanceId = "not-used",
        };
        var eventHubEventData = new EventData(invalidNotifyFromMeasurements.ToByteArray());

        // When
        await ProcessManagerEventHubProducerClient.SendAsync([eventHubEventData], CancellationToken.None);

        // Then
        var expectedFunctionName = nameof(EnqueueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1);

        var wasExecutedExpectedTimes = await Awaiter.TryWaitUntilConditionAsync(
            () =>
            {
                var executedFailedLogs = _fixture.OrchestrationsAppManager.AppHostManager
                    .GetHostLogSnapshot()
                    .Where(log => log.Contains($"Executed 'Functions.{expectedFunctionName}' (Failed", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return executedFailedLogs.Count > 1;
            },
            timeLimit: TimeSpan.FromSeconds(20),
            delay: TimeSpan.FromSeconds(3));

        wasExecutedExpectedTimes.Should().BeTrue("because we expected the trigger to be executed at least twice because of the configured 'ExponentialBackoffRetry' retry policy");
    }

    private static ForwardMeteredDataInputV1 CreateForwardMeteredDataInputV1(bool isDelegation = false)
    {
        var sender = isDelegation ? DelegatedToGridAccessProvider : GridAccessProvider;
        var input = new ForwardMeteredDataInputV1(
            ActorMessageId: "MessageId",
            TransactionId: "EGU9B8E2630F9CB4089BDE22B597DFA4EA5",
            ActorNumber: sender,
            ActorRole: ActorRole.MeteredDataResponsible.Name,
            BusinessReason: BusinessReason.PeriodicMetering.Name,
            MeteringPointId: MeteringPointId,
            MeteringPointType: MeteringPointType.Production.Name,
            ProductNumber: "8716867000047",
            MeasureUnit: MeasurementUnit.KilowattHour.Name,
            RegistrationDateTime: "2024-12-03T08:00:00Z",
            Resolution: Resolution.Hourly.Name,
            StartDateTime: "2024-12-01T23:00:00Z",
            EndDateTime: "2024-12-02T23:00:00Z",
            GridAccessProviderNumber: sender,
            MeteredDataList:
            [
                new("1", "112.000", Quality.AsProvided.Name),
                new("2", "112.000", Quality.AsProvided.Name),
                new("3", "112.000", Quality.AsProvided.Name),
                new("4", "112.000", Quality.AsProvided.Name),
                new("5", "112.000", Quality.AsProvided.Name),
                new("6", "112.000", Quality.AsProvided.Name),
                new("7", "112.000", Quality.AsProvided.Name),
                new("8", "112.000", Quality.AsProvided.Name),
                new("9", "112.000", Quality.AsProvided.Name),
                new("10", "112.000", Quality.AsProvided.Name),
                new("11", "112.000", Quality.AsProvided.Name),
                new("12", "112.000", Quality.AsProvided.Name),
                new("13", "112.000", Quality.AsProvided.Name),
                new("14", "112.000", Quality.AsProvided.Name),
                new("15", "112.000", Quality.AsProvided.Name),
                new("16", "112.000", Quality.AsProvided.Name),
                new("17", "112.000", Quality.AsProvided.Name),
                new("18", "112.000", Quality.AsProvided.Name),
                new("19", "112.000", Quality.AsProvided.Name),
                new("20", "112.000", Quality.AsProvided.Name),
                new("21", "112.000", Quality.AsProvided.Name),
                new("22", "112.000", Quality.AsProvided.Name),
                new("23", "112.000", Quality.AsProvided.Name),
                new("24", "112.000", Quality.AsProvided.Name),
            ]);
        return input;
    }

    private void SetupElectricityMarketWireMocking()
    {
        var request = Request
            .Create()
            .WithPath("/api/get-metering-point-master-data")
            .WithBody(_ => true)
            .UsingPost();

        var meteringPointMasterData = new ElectricityMarket.Integration.Models.MasterData.MeteringPointMasterData
        {
            Identification = new ElectricityMarketModels.MeteringPointIdentification(MeteringPointId),
            ValidFrom = _validFrom,
            ValidTo = _validTo,
            GridAreaCode = new ElectricityMarket.Integration.Models.MasterData.GridAreaCode(GridArea),
            GridAccessProvider = GridAccessProvider,
            NeighborGridAreaOwners = [NeighborGridAreaOwner1, NeighborGridAreaOwner2],
            ConnectionState = ElectricityMarket.Integration.Models.MasterData.ConnectionState.Connected,
            Type = ElectricityMarket.Integration.Models.MasterData.MeteringPointType.Production,
            SubType = ElectricityMarket.Integration.Models.MasterData.MeteringPointSubType.Physical,
            Resolution = new ElectricityMarket.Integration.Models.MasterData.Resolution("PT1H"),
            Unit = ElectricityMarketModels.MeasureUnit.kWh,
            ProductId = ElectricityMarketModels.ProductId.Tariff,
            ParentIdentification = null,
            EnergySupplier = EnergySupplier,
        };

        // IEnumerable<HistoricalMeteringPointMasterData>
        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(
                $"[{JsonSerializer.Serialize(meteringPointMasterData, new JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb))}]");

        _fixture.OrchestrationsAppManager.MockServer.Given(request).RespondWith(response);
    }

    private void SetupElectricityMarketDelegationWireMocking()
    {
        var request = Request
            .Create()
            .WithPath("/api/get-process-delegation")
            .WithBody(_ => true)
            .UsingPost();

        var delegationFrom = new ProcessDelegationDto(
            DelegatedToGridAccessProvider,
            ActorRole: EicFunction.Delegated);

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(
                $"{JsonSerializer.Serialize(delegationFrom, new JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb))}");

        _fixture.OrchestrationsAppManager.MockServer.Given(request).RespondWith(response);
    }
}
