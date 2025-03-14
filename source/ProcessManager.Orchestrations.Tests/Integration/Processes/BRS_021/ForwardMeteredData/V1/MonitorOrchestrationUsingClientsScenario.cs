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
using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.Core.FunctionApp.TestCommon.EventHub.ListenerMock;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Triggers;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Google.Protobuf;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;
using MeteringPointType = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.MeteringPointType;
using Quality = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Quality;
using Resolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// forward metered data flow
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private const string ProcessManagerEventHubProducerClientName = "ProcessManagerEventHubProducerClient";
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
            // Process Manager HTTP client
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                = AuthenticationOptionsForTests.ApplicationIdUri,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = _fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = _fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),

            // Measurements Eventhub client
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.EventHubName)}"]
                = _fixture.OrchestrationsAppManager.MeasurementEventHubName,
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.FullyQualifiedNamespace)}"]
                = _fixture.IntegrationTestConfiguration.EventHubFullyQualifiedNamespace,

            // Process Manager Eventhub client to simulate the notification event from measurements
            [$"{ProcessManagerEventHubOptions.SectionName}:{nameof(ProcessManagerEventHubOptions.EventHubName)}"]
                = _fixture.OrchestrationsAppManager.ProcessManagerEventhubName,
            [$"{ProcessManagerEventHubOptions.SectionName}:{nameof(ProcessManagerEventHubOptions.FullyQualifiedNamespace)}"]
                = _fixture.IntegrationTestConfiguration.EventHubFullyQualifiedNamespace,

            // Process Manager message client
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.StartTopicName)}"]
                = _fixture.OrchestrationsAppManager.ProcessManagerStartTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.NotifyTopicName)}"]
                = _fixture.ProcessManagerAppManager.ProcessManagerNotifyTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataStartTopicName)}"]
                = _fixture.OrchestrationsAppManager.Brs021ForwardMeteredDataStartTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataNotifyTopicName)}"]
                = _fixture.OrchestrationsAppManager.Brs021ForwardMeteredDataNotifyTopic.Name,
        });
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(_fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerMessageClient();
        services.AddProcessManagerHttpClients();

        services
            .AddOptions<ProcessManagerEventHubOptions>()
            .BindConfiguration(ProcessManagerEventHubOptions.SectionName)
            .ValidateDataAnnotations();

        // Add event hub producer client for ProcessManagerEventHub to simulate the notification event from measurements
        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<EventHubProducerClient, EventHubProducerClientOptions>(
                        (_, _, provider) =>
                        {
                            var options = provider.GetRequiredService<IOptions<ProcessManagerEventHubOptions>>()
                                .Value;
                            return new EventHubProducerClient(
                                $"{options.FullyQualifiedNamespace}",
                                options.EventHubName,
                                _fixture.IntegrationTestConfiguration.Credential);
                        })
                    .WithName(ProcessManagerEventHubProducerClientName);
            });
        ServiceProvider = services.BuildServiceProvider();
    }

    private ServiceProvider ServiceProvider { get; }

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
    public async Task Given_ValidForwardMeteredDataInputV1_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        // Arrange
        var input = CreateForwardMeteredDataInputV1();

        var forwardCommand = new ForwardMeteredDataCommandV1(
            new ActorIdentityDto(ActorNumber.Create(input.ActorNumber), ActorRole.FromName(input.ActorRole)),
            input,
            idempotencyKey: Guid.NewGuid().ToString());

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
        var eventHubClientFactory = ServiceProvider.GetRequiredService<IAzureClientFactory<EventHubProducerClient>>();

        // Act
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(forwardCommand, CancellationToken.None);

        // Step 2a: Query until waiting for Event Hub notify event from Measurements
        var (isWaitingForMeasurementsNotify, orchestrationInstance) = await processManagerClient
            .WaitForStepToBeRunning<ForwardMeteredDataInputV1>(
                forwardCommand.IdempotencyKey,
                OrchestrationDescriptionBuilderV1.ForwardToMeasurementsStep);

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
        var processManagerEventHubProducerClient = eventHubClientFactory.CreateClient(ProcessManagerEventHubProducerClientName);
        await processManagerEventHubProducerClient.SendAsync([eventHubEventData], CancellationToken.None);

        // Wait for enqueue messages sent to EDI and send mock notify response to Process Manager
        await _fixture.EnqueueBrs021ForwardMeteredDataServiceBusListener.WaitOnEnqueueMessagesInEdiAndMockNotifyToProcessManager(
            processManagerMessageClient: processManagerMessageClient,
            orchestrationInstanceId: orchestrationInstance.Id,
            messageId: forwardCommand.ActorMessageId);

        // Query until terminated
        var (orchestrationTerminatedWithSucceeded, terminatedOrchestrationInstance) = await processManagerClient
            .WaitForOrchestrationInstanceTerminated<ForwardMeteredDataInputV1>(
                idempotencyKey: forwardCommand.IdempotencyKey);

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
                        .And.Be(OrchestrationStepTerminationState.Succeeded);
                });
    }

    [Fact]
    public async Task Given_InvalidForwardMeteredDataInputV1_When_Started_Then_OrchestrationInstanceTerminatesWithFailed_AndThen_BusinessValidationStepFailed()
    {
        // Given
        var invalidInput = CreateForwardMeteredDataInputV1() with { EndDateTime = null };

        var invalidForwardCommand = new ForwardMeteredDataCommandV1(
            new ActorIdentityDto(ActorNumber.Create(invalidInput.ActorNumber), ActorRole.FromName(invalidInput.ActorRole)),
            invalidInput,
            idempotencyKey: Guid.NewGuid().ToString());

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // When
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(invalidForwardCommand, CancellationToken.None);

        // Then
        // Query until waiting for EnqueueActorMessagesCompleted notify event (a reject message should be enqueued)
        var (isWaitingForNotify, orchestrationInstance) = await processManagerClient
            .WaitForStepToBeRunning<ForwardMeteredDataInputV1>(
                invalidForwardCommand.IdempotencyKey,
                OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep);

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
                        .HaveCount(1)
                        .And.ContainSingle(
                            (e) => e.Message.Equals(PeriodValidationRule.InvalidEndDate.Message));
                    return forwardMeteredDataRejectedV1.OriginalTransactionId == invalidForwardCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueRejectedActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue($"because a {nameof(ForwardMeteredDataRejectedV1)} service bus message should have been sent");

        // Send EnqueueActorMessagesCompleted event
        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new ForwardMeteredDataNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstance!.Id.ToString()),
            CancellationToken.None);

        // Query until terminated
        var (orchestrationTerminatedWithSucceeded, terminatedOrchestrationInstance) = await processManagerClient
            .WaitForOrchestrationInstanceTerminated<ForwardMeteredDataInputV1>(
                idempotencyKey: invalidForwardCommand.IdempotencyKey);

        orchestrationTerminatedWithSucceeded.Should().BeTrue(
            "because the orchestration instance should be terminated within given wait time");

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
                    s.Sequence.Should().Be(OrchestrationDescriptionBuilderV1.BusinessValidationStep);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(OrchestrationStepTerminationState.Failed);
                },
                s =>
                {
                    // Forward to measurements step should be skipped
                    s.Sequence.Should().Be(OrchestrationDescriptionBuilderV1.ForwardToMeasurementsStep);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(OrchestrationStepTerminationState.Skipped);
                },
                s =>
                {
                    // Find receiver step should be skipped
                    s.Sequence.Should().Be(OrchestrationDescriptionBuilderV1.FindReceiverStep);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(OrchestrationStepTerminationState.Skipped);
                },
                s =>
                {
                    // Enqueue actor messages step should be succeeded
                    s.Sequence.Should().Be(OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(OrchestrationStepTerminationState.Succeeded);
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
        var eventHubClientFactory = ServiceProvider.GetRequiredService<IAzureClientFactory<EventHubProducerClient>>();
        var processManagerEventHubProducerClient = eventHubClientFactory.CreateClient(ProcessManagerEventHubProducerClientName);

        var invalidNotifyFromMeasurements = new Brs021ForwardMeteredDataNotifyV1()
        {
            Version = "invalid-value",
            OrchestrationInstanceId = "not-used",
        };
        var eventHubEventData = new EventData(invalidNotifyFromMeasurements.ToByteArray());

        // When
        await processManagerEventHubProducerClient.SendAsync([eventHubEventData], CancellationToken.None);

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

    private static ForwardMeteredDataInputV1 CreateForwardMeteredDataInputV1()
    {
        var input = new ForwardMeteredDataInputV1(
            ActorMessageId: "MessageId",
            TransactionId: "EGU9B8E2630F9CB4089BDE22B597DFA4EA5",
            ActorNumber: "1111111111111",
            ActorRole: ActorRole.GridAccessProvider.Name,
            MeteringPointId: "571313101700011887",
            MeteringPointType: MeteringPointType.Production.Name,
            ProductNumber: "8716867000047",
            MeasureUnit: MeasurementUnit.MetricTon.Name,
            RegistrationDateTime: "2024-12-03T08:00:00Z",
            Resolution: Resolution.Hourly.Name,
            StartDateTime: "2024-12-01T23:00:00Z",
            EndDateTime: "2024-12-02T23:00:00Z",
            GridAccessProviderNumber: "5790002606892",
            DelegatedGridAreaCodes: null,
            EnergyObservations:
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
}
