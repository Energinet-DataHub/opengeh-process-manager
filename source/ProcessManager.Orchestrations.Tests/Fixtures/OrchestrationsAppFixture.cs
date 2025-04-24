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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.Core.FunctionApp.TestCommon;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.EventHub.ListenerMock;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

/// <summary>
/// Support testing the interactions with ProcessManager.Orchestrations and
/// Process Manager Api, by coordinating the startup of the dependent applications
/// ProcessManager.Orchestrations and ProcessManager (Api).
/// </summary>
public class OrchestrationsAppFixture : IAsyncLifetime
{
    private const string TaskHubName = "OrchestrationsTest01";
    private const string MeasurementEventHubName = "eventhub-measurements";

    public OrchestrationsAppFixture()
    {
        DatabaseManager = new ProcessManagerDatabaseManager("OrchestrationsTests");
        AzuriteManager = new AzuriteManager(useOAuth: true);
        DurableTaskManager = new DurableTaskManager(
            "AzuriteConnectionString",
            AzuriteManager.FullConnectionString);

        IntegrationTestConfiguration = new IntegrationTestConfiguration();

        OrchestrationsAppManager = new OrchestrationsAppManager(
            DatabaseManager,
            IntegrationTestConfiguration,
            AzuriteManager,
            taskHubName: TaskHubName,
            appPort: 8101,
            wireMockServerPort: 8112,
            manageDatabase: false,
            manageAzurite: false,
            measurementEventHubName: MeasurementEventHubName);

        ProcessManagerAppManager = new ProcessManagerAppManager(
            DatabaseManager,
            IntegrationTestConfiguration,
            AzuriteManager,
            taskHubName: TaskHubName,
            appPort: 8102,
            manageDatabase: false,
            manageAzurite: false);

        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            OrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);

        EnqueueBrs021ForwardMeteredDataServiceBusListener = new ServiceBusListenerMock(
            OrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
        EnqueueBrs023027ServiceBusListener = new ServiceBusListenerMock(
            OrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
        EnqueueBrs026ServiceBusListener = new ServiceBusListenerMock(
            OrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
        EnqueueBrs028ServiceBusListener = new ServiceBusListenerMock(
            OrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);

        IntegrationEventServiceBusListener = new ServiceBusListenerMock(
            OrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    public OrchestrationsAppManager OrchestrationsAppManager { get; }

    public ProcessManagerAppManager ProcessManagerAppManager { get; }

    [NotNull]
    public EventHubListenerMock? EventHubListener { get; private set; }

    [NotNull]
    public IDurableClient? DurableClient { get; private set; }

    public ServiceBusListenerMock EnqueueBrs021ForwardMeteredDataServiceBusListener { get; }

    public ServiceBusListenerMock EnqueueBrs023027ServiceBusListener { get; }

    public ServiceBusListenerMock EnqueueBrs026ServiceBusListener { get; }

    public ServiceBusListenerMock EnqueueBrs028ServiceBusListener { get; }

    public ServiceBusListenerMock IntegrationEventServiceBusListener { get; }

    public ActorIdentityDto DefaultActorIdentity => new ActorIdentityDto(
        ActorNumber.Create("1234567890123"),
        ActorRole.EnergySupplier);

    public UserIdentityDto DefaultUserIdentity => new UserIdentityDto(
        Guid.NewGuid(),
        DefaultActorIdentity.ActorNumber,
        DefaultActorIdentity.ActorRole);

    private ProcessManagerDatabaseManager DatabaseManager { get; }

    private AzuriteManager AzuriteManager { get; }

    private DurableTaskManager DurableTaskManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    public async Task InitializeAsync()
    {
        AzuriteManager.CleanupAzuriteStorage();
        AzuriteManager.StartAzurite();

        await DatabaseManager.CreateDatabaseAsync();

        DurableClient = DurableTaskManager.CreateClient(TaskHubName);

        // Start Process Manager app
        // => Creates Process Manager default Notify topic and subscription
        await ProcessManagerAppManager.StartAsync();

        // Creates EDI enqueue actor messages topic and subscriptions
        var ediEnqueueTopicResources = await OrchestrationsAppManager.EdiEnqueueTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        // => Create listeners for enqueue messages
        await EnqueueBrs021ForwardMeteredDataServiceBusListener.AddTopicSubscriptionListenerAsync(
            ediEnqueueTopicResources.Brs021ForwardMeteredDataSubscription.TopicName,
            ediEnqueueTopicResources.Brs021ForwardMeteredDataSubscription.SubscriptionName);
        await EnqueueBrs023027ServiceBusListener.AddTopicSubscriptionListenerAsync(
            ediEnqueueTopicResources.Brs023027Subscription.TopicName,
            ediEnqueueTopicResources.Brs023027Subscription.SubscriptionName);
        await EnqueueBrs026ServiceBusListener.AddTopicSubscriptionListenerAsync(
            ediEnqueueTopicResources.Brs026Subscription.TopicName,
            ediEnqueueTopicResources.Brs026Subscription.SubscriptionName);
        await EnqueueBrs028ServiceBusListener.AddTopicSubscriptionListenerAsync(
            ediEnqueueTopicResources.Brs028Subscription.TopicName,
            ediEnqueueTopicResources.Brs028Subscription.SubscriptionName);

        // Create Integration Event topic resources
        var integrationEventTopicResources = await OrchestrationsAppManager.IntegrationEventTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        await IntegrationEventServiceBusListener.AddTopicSubscriptionListenerAsync(
            integrationEventTopicResources.SharedTopic.Name,
            integrationEventTopicResources.Subscription.SubscriptionName);

        // Start Process Manager Orchestrations app
        // => Creates Process Manager default Start topics and subscriptions
        // => Creates BRS-021 Forward Metered Data Start/Notify topics and subscriptions
        await OrchestrationsAppManager.StartAsync(ediEnqueueTopicResources, integrationEventTopicResources);

        EventHubListener = new EventHubListenerMock(
            testLogger: new TestDiagnosticsLogger(),
            eventHubFullyQualifiedNamespace: IntegrationTestConfiguration.EventHubFullyQualifiedNamespace,
            eventHubName: OrchestrationsAppManager.MeasurementEventHubName,
            blobStorageServiceUri: AzuriteManager.BlobStorageServiceUri,
            blobContainerName: "container-01",
            credential: IntegrationTestConfiguration.Credential);
        await EventHubListener.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await EventHubListener.DisposeAsync();
        await OrchestrationsAppManager.DisposeAsync();
        await ProcessManagerAppManager.DisposeAsync();
        await DurableTaskManager.DisposeAsync();
        await DatabaseManager.DeleteDatabaseAsync();
        AzuriteManager.Dispose();
        await ServiceBusResourceProvider.DisposeAsync();
        await EnqueueBrs023027ServiceBusListener.DisposeAsync();
        await EnqueueBrs026ServiceBusListener.DisposeAsync();
        await EnqueueBrs028ServiceBusListener.DisposeAsync();
        await IntegrationEventServiceBusListener.DisposeAsync();
    }

    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        OrchestrationsAppManager.SetTestOutputHelper(testOutputHelper);
        ProcessManagerAppManager.SetTestOutputHelper(testOutputHelper);
    }
}
