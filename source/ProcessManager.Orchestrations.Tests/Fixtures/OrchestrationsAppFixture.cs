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
using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.EventHub.ListenerMock;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

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
            // TODO (ID-283)
            environment: "IntegrationTests",
            eventHubName: MeasurementEventHubName);

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

        EnqueueBrs023027ServiceBusListener = new ServiceBusListenerMock(
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

    [NotNull]
    public string? ProcessManagerTopicName { get; private set; }

    public ServiceBusListenerMock EnqueueBrs023027ServiceBusListener { get; }

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

        // Create a shared Process Manager topic with subscriptions for both Orchestrations and Process Manager apps.
        var processManagerTopicResourceBuilder = OrchestrationsAppManager.ProcessManagerTopicResources
                .BuildProcessManagerTopic(ServiceBusResourceProvider);
        OrchestrationsAppManager.ProcessManagerTopicResources.AddOrchestrationsAppSubscriptions(processManagerTopicResourceBuilder);
        ProcessManagerAppManager.ProcessManagerTopicResources.AddProcessManagerAppSubscriptions(processManagerTopicResourceBuilder);
        var processManagerTopicResource = await processManagerTopicResourceBuilder.CreateAsync();

        // Get resources from the created Process Manager topic for both Orchestrations and Process Manager apps.
        var orchestrationsProcessManagerTopicResources = OrchestrationsAppManager.ProcessManagerTopicResources
            .GetProcessManagerTopicResources(processManagerTopicResource);
        var processManagerAppProcessManagerTopicResources = ProcessManagerAppManager.ProcessManagerTopicResources
            .GetProcessManagerTopicResources(processManagerTopicResource);

        // Create EDI topic resources
        var ediTopicResources = await OrchestrationsAppManager.EdiTopicResources.Create(ServiceBusResourceProvider);

        await EnqueueBrs023027ServiceBusListener.AddTopicSubscriptionListenerAsync(
            ediTopicResources.EnqueueBrs023027Subscription.TopicName,
            ediTopicResources.EnqueueBrs023027Subscription.SubscriptionName);

        await OrchestrationsAppManager.StartAsync(orchestrationsProcessManagerTopicResources, ediTopicResources);
        await ProcessManagerAppManager.StartAsync(processManagerAppProcessManagerTopicResources);

        ProcessManagerTopicName = orchestrationsProcessManagerTopicResources.ProcessManagerTopic.Name;

        EventHubListener = new EventHubListenerMock(
            new TestDiagnosticsLogger(),
            IntegrationTestConfiguration.EventHubFullyQualifiedNamespace,
            eventHubName: OrchestrationsAppManager.EventHubName,
            AzuriteManager.BlobStorageServiceUri,
            blobContainerName: "container-01",
            IntegrationTestConfiguration.Credential);
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
    }

    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        OrchestrationsAppManager.SetTestOutputHelper(testOutputHelper);
        ProcessManagerAppManager.SetTestOutputHelper(testOutputHelper);
    }
}
