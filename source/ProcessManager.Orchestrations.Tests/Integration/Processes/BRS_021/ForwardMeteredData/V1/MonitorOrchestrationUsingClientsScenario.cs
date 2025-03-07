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
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;
using MeteringPointType = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.MeteringPointType;
using Resolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// forward metered data flow
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private readonly OrchestrationsAppFixture _fixture;
    private readonly string _processManagerEventHubProducerClientName = "ProcessManagerEventHubProducerClient";

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
                    .WithName(_processManagerEventHubProducerClientName);
            });
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

    [Fact(Skip = "This test is not yet implemented")]
    public async Task ForwardMeteredData_WhenStartedUsingCorrectInput_ThenExecutedHappyPath()
    {
        // Arrange
        var input = CreateMeteredDataForMeteringPointMessageInputV1();

        var startCommand = new ForwardMeteredDataCommandV1(
            new ActorIdentityDto(ActorNumber.Create(input.ActorNumber), ActorRole.FromName(input.ActorRole)),
            input,
            idempotencyKey: Guid.NewGuid().ToString());

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
        var eventHubClientFactory = ServiceProvider.GetRequiredService<IAzureClientFactory<EventHubProducerClient>>();

        // Act
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(startCommand, CancellationToken.None);

        var orchestrationCreatedAfter = DateTime.UtcNow.AddSeconds(-1);
        await Awaiter.WaitUntilConditionAsync(
            async () =>
            {
                var instances = await SearchAsync(processManagerClient, orchestrationCreatedAfter);

                return instances.Count >= 1;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(2));

        var instances = await SearchAsync(processManagerClient, orchestrationCreatedAfter);
        var instance = instances.Should().ContainSingle().Subject;

        // Verify that an persistSubmittedTransaction event is sent on the event hub
        var verifyForwardMeteredDataToMeasurementsEvent = await _fixture.EventHubListener.When(
                (message) =>
                {
                    var persistSubmittedTransaction =
                        PersistSubmittedTransaction.Parser.ParseFrom(message.EventBody.ToArray());

                    var orchestrationIdMatches = persistSubmittedTransaction.OrchestrationInstanceId == instance.Id.ToString();
                    var transactionIdMatches = persistSubmittedTransaction.TransactionId == input.TransactionId;

                    return orchestrationIdMatches && transactionIdMatches;
                })
            .VerifyCountAsync(1);

        var persistSubmittedTransactionEventFound = verifyForwardMeteredDataToMeasurementsEvent.Wait(TimeSpan.FromSeconds(30));
        persistSubmittedTransactionEventFound.Should().BeTrue($"because a {nameof(PersistSubmittedTransaction)} event should have been sent");

        // Send a notification to the Process Manager Event Hub to simulate the notification event from measurements
        var notify = new Brs021ForwardMeteredDataNotifyV1()
        {
            OrchestrationInstanceId = instance.Id.ToString(),
        };

        var data = new EventData(notify.ToByteArray());
        var processManagerEventHubProducerClient =
            eventHubClientFactory.CreateClient(_processManagerEventHubProducerClientName);
        await processManagerEventHubProducerClient.SendAsync([data], CancellationToken.None);

        // wait for notification from edi.
        // TODO: Refactor this to use _fixture.EnqueueBrs021ForwardMeteredDataServiceBusListener.When()
        await _fixture.EnqueueBrs021ForwardMeteredDataServiceBusListener.WaitOnEnqueueMessagesInEdiAndMockNotifyToProcessManager(
            processManagerMessageClient: processManagerMessageClient,
            orchestrationInstanceId: instance.Id,
            messageId: startCommand.ActorMessageId);

        // TODO: Fetch the terminated instance and assert that it has been terminated successfully
        await Task.Delay(TimeSpan.FromSeconds(5));

        var simulateTheTerminatedInstance = await processManagerClient.SearchOrchestrationInstancesByNameAsync(
            new SearchOrchestrationInstancesByNameQuery(
                _fixture.DefaultUserIdentity,
                name: Brs_021_ForwardedMeteredData.Name,
                version: null,
                lifecycleStates: null,
                terminationState: null,
                startedAtOrLater: orchestrationCreatedAfter,
                terminatedAtOrEarlier: null,
                scheduledAtOrLater: null),
            CancellationToken.None);

        var instanceAfterEnqueue = simulateTheTerminatedInstance.Should().ContainSingle().Subject;

        var stepsWhichShouldBeSuccessful = new[]
        {
            OrchestrationDescriptionBuilderV1.ValidationStep,
            OrchestrationDescriptionBuilderV1.ForwardToMeasurementStep,
            OrchestrationDescriptionBuilderV1.FindReceiverStep,
            // TODO: re-enable when the Process Manager Client can send notifications to the Brs021 topic
            //OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep,
        };

        var successfulSteps = instanceAfterEnqueue.Steps
            .Where(step => step.Lifecycle.TerminationState is OrchestrationStepTerminationState.Succeeded)
            .Select(step => step.Sequence);

        successfulSteps.Should().BeEquivalentTo(stepsWhichShouldBeSuccessful);

        var searchResult = await processManagerClient.SearchOrchestrationInstancesByNameAsync(
             new SearchOrchestrationInstancesByNameQuery(
                 _fixture.DefaultUserIdentity,
                 name: Brs_021_ForwardedMeteredData.Name,
                 version: null,
                 // TODO: switch to lifecycleStates: [OrchestrationInstanceLifecycleState.Terminated] when the Process Manager Client can send notifications to the Brs021 topic
                 lifecycleStates: [OrchestrationInstanceLifecycleState.Running],
                 // TODO: switch to terminationState: OrchestrationInstanceTerminationState.Succeeded when the Process Manager Client can send notifications to the Brs021 topic
                 terminationState: null,
                 startedAtOrLater: orchestrationCreatedAfter,
                 terminatedAtOrEarlier: null,
                 scheduledAtOrLater: null),
             CancellationToken.None);

        searchResult.Should().NotBeNull().And.ContainSingle();
        searchResult.Single().Steps.Should().HaveCount(4);
        // TODO: re-enable when the Process Manager Client can send notifications to the Brs021 topic
        // searchResult.Single().Steps.Should().AllSatisfy(
        //  step => step.Lifecycle.TerminationState.Should().Be(OrchestrationStepTerminationState.Succeeded));
        // TODO: Assert that the orchestration instance has been terminated successfully
    }

    private static ForwardMeteredDataInputV1 CreateMeteredDataForMeteringPointMessageInputV1(
        bool withError = false)
    {
        var input = new ForwardMeteredDataInputV1(
            "MessageId",
            Guid.NewGuid(),
            "1111111111111",
            ActorRole.GridAccessProvider.Name,
            "EGU9B8E2630F9CB4089BDE22B597DFA4EA5",
            withError ? "NoMasterData" : "571313101700011887",
            MeteringPointType.Production.Name,
            "8716867000047",
            MeasurementUnit.MetricTon.Name,
            "2024-12-03T08:00:00Z",
            Resolution.Hourly.Name,
            "2024-12-01T23:00:00Z",
            "2024-12-02T23:00:00Z",
            "5790002606892",
            null,
            [
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
            ]);
        return input;
    }

    private async Task<IReadOnlyCollection<OrchestrationInstanceTypedDto>> SearchAsync(IProcessManagerClient processManagerClient, DateTime orchestrationCreatedAfter)
    {
        return await processManagerClient.SearchOrchestrationInstancesByNameAsync(
            new SearchOrchestrationInstancesByNameQuery(
                _fixture.DefaultUserIdentity,
                name: Brs_021_ForwardedMeteredData.Name,
                version: null,
                lifecycleStates: [OrchestrationInstanceLifecycleState.Running],
                terminationState: null,
                startedAtOrLater: orchestrationCreatedAfter,
                terminatedAtOrEarlier: null,
                scheduledAtOrLater: null),
            CancellationToken.None);
    }
}
