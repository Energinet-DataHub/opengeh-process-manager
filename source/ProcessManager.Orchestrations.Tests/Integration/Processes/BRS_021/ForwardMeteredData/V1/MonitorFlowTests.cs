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

using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// forward metered data flow
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorFlowTests : IAsyncLifetime
{
    private const string ProcessManagerEventHubName = "process-manager-event-hub";

    private readonly OrchestrationsAppFixture _fixture;

    public MonitorFlowTests(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            // Service bus client
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"]
                = _fixture.ProcessManagerTopicName,
            // Https client
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                = AuthenticationOptionsForTests.ApplicationIdUri,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = _fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = _fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),

            // Eventhub client
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.NamespaceName)}"]
                = _fixture.IntegrationTestConfiguration.EventHubNamespaceName,
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.EventHubName)}"]
                = _fixture.OrchestrationsAppManager.EventHubName,
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.ProcessManagerEventHubName)}"]
                = _fixture.OrchestrationsAppManager.ProcessManagerEventhubName,
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.FullyQualifiedNamespace)}"]
                = _fixture.IntegrationTestConfiguration.EventHubFullyQualifiedNamespace,
        });
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(_fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerMessageClient();
        services.AddProcessManagerHttpClients();
        services.AddMeasurementsMeteredDataClient(_fixture.IntegrationTestConfiguration.Credential);
        services.AddAzureClients(
            builder =>
            {
                builder.AddClient<EventHubProducerClient, EventHubProducerClientOptions>(
                        (_, _, provider) =>
                        {
                            var options = provider.GetRequiredService<IOptions<MeasurementsMeteredDataClientOptions>>().Value;
                            return new EventHubProducerClient(
                                $"{options.FullyQualifiedNamespace}",
                                options.ProcessManagerEventHubName,
                                _fixture.IntegrationTestConfiguration.Credential);
                        })
                    .WithName(ProcessManagerEventHubName);
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

    [Fact]
    public async Task ForwardMeteredData_WhenStartedUsingCorrectInput_ThenExecutedHappyPath()
    {
        // Arrange
        var input = CreateMeteredDataForMeteringPointMessageInputV1();

        var startCommand = new StartForwardMeteredDataCommandV1(
            new ActorIdentityDto(ActorNumber.Create(input.ActorNumber), ActorRole.FromName(input.ActorRole)),
            input,
            idempotencyKey: Guid.NewGuid().ToString());

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
        var eventHubClientFactory = ServiceProvider.GetRequiredService<IAzureClientFactory<EventHubProducerClient>>();

        // Act
        var orchestrationCreatedAfter = DateTime.UtcNow.AddSeconds(-1);
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(startCommand, CancellationToken.None);

        await Awaiter.WaitUntilConditionAsync(
            async () =>
            {
                var instances = await SearchAsync(processManagerClient, orchestrationCreatedAfter);

                return instances.Count >= 1;
            },
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1));

        var instances = await SearchAsync(processManagerClient, orchestrationCreatedAfter);
        var instance = instances.Should().ContainSingle().Subject;

        // Wait for eventhub trigger
        var success = await _fixture.EventHubListener.AssertAndMockEventHubMessageToAndFromMeasurementsAsync(
            eventHubProducerClient: eventHubClientFactory.CreateClient(ProcessManagerEventHubName),
            orchestrationInstanceId: instance.Id,
            transactionId: input.TransactionId,
            shouldSendNotify: true);

        success.Should().Be(true);

        // wait for notification from edi.
        await _fixture.EnqueueBrs021ForwardMeteredDataServiceBusListener.WaitAndMockServiceBusMessageToAndFromEdi(
            processManagerMessageClient: processManagerMessageClient,
            orchestrationInstanceId: instance.Id,
            messageId: startCommand.ActorMessageId);

        await Task.Delay(TimeSpan.FromSeconds(5));

        var instancesAfterEnqueue = await processManagerClient.SearchOrchestrationInstancesByNameAsync(
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

        var instanceAfterEnqueue = instancesAfterEnqueue.Should().ContainSingle().Subject;

        var stepsWhichShouldBeSuccessful = new[]
        {
            OrchestrationDescriptionBuilderV1.ValidatingStep,
            OrchestrationDescriptionBuilderV1.ForwardToMeasurementStep,
            OrchestrationDescriptionBuilderV1.FindReceiverStep,
        };

        var successfulSteps = instanceAfterEnqueue.Steps
            .Where(step => step.Lifecycle.TerminationState is Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance.OrchestrationStepTerminationState.Succeeded)
            .Select(step => step.Sequence);

        successfulSteps.Should().BeEquivalentTo(stepsWhichShouldBeSuccessful);
        instanceAfterEnqueue.Steps
            .First(step => step.Sequence == OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep)
            .Lifecycle.State.Should()
            .Be(StepInstanceLifecycleState.Running);

        /*
        var searchResult = await processManagerClient.SearchOrchestrationInstancesByNameAsync(
            new SearchOrchestrationInstancesByNameQuery(
                _fixture.DefaultUserIdentity,
                name: Brs_021_ForwardedMeteredData.Name,
                version: null,
                lifecycleStates: [OrchestrationInstanceLifecycleState.Terminated],
                terminationState: OrchestrationInstanceTerminationState.Succeeded,
                startedAtOrLater: orchestrationCreatedAfter,
                terminatedAtOrEarlier: null,
                scheduledAtOrLater: null),
            CancellationToken.None);

        searchResult.Should().NotBeNull().And.ContainSingle();
        searchResult.Single().Steps.Should().HaveCount(4);
        searchResult.Single().Steps.Should().AllSatisfy(
            step => step.Lifecycle.TerminationState.Should().Be(OrchestrationStepTerminationState.Succeeded));
            */
    }

    private static MeteredDataForMeteringPointMessageInputV1 CreateMeteredDataForMeteringPointMessageInputV1(
        bool withError = false)
    {
        var input = new MeteredDataForMeteringPointMessageInputV1(
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

    private async Task<IReadOnlyCollection<OrchestrationInstanceTypedDto>> SearchAsync(IProcessManagerClient processManagerClient, DateTime orchestrationCreatedAfter)
    {
        return await processManagerClient.SearchOrchestrationInstancesByNameAsync(
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
    }
}
