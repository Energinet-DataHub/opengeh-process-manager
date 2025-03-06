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
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;

/// <summary>
/// Support testing the interactions with ProcessManager.Example.Orchestrations and
/// Process Manager Api, by coordinating the startup of the dependent applications
/// ProcessManager.Example.Orchestrations and ProcessManager (Api).
/// </summary>
public class ExampleOrchestrationsAppFixture : IAsyncLifetime
{
    private const string TaskHubName = "ExampleOrchestrationsAppTest01";

    public ExampleOrchestrationsAppFixture()
    {
        DatabaseManager = new ProcessManagerDatabaseManager("ExampleOrchestrationsAppTests");
        AzuriteManager = new AzuriteManager(useOAuth: true);

        IntegrationTestConfiguration = new IntegrationTestConfiguration();

        ExampleOrchestrationsAppManager = new ExampleOrchestrationsAppManager(
            DatabaseManager,
            IntegrationTestConfiguration,
            AzuriteManager,
            taskHubName: TaskHubName,
            appPort: 8301,
            manageDatabase: false,
            manageAzurite: false);

        ProcessManagerAppManager = new ProcessManagerAppManager(
            DatabaseManager,
            IntegrationTestConfiguration,
            AzuriteManager,
            taskHubName: TaskHubName,
            appPort: 8302,
            manageDatabase: false,
            manageAzurite: false);

        ExampleConsumerAppManager = new ExampleConsumerAppManager(
            IntegrationTestConfiguration,
            appPort: 8303);

        DurableTaskManager = new DurableTaskManager(
            "AzuriteConnectionString",
            AzuriteManager.FullConnectionString);

        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            ExampleOrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    public ExampleOrchestrationsAppManager ExampleOrchestrationsAppManager { get; }

    public ProcessManagerAppManager ProcessManagerAppManager { get; }

    public ExampleConsumerAppManager ExampleConsumerAppManager { get; }

    [NotNull]
    public IDurableClient? DurableClient { get; private set; }

    [NotNull]
    public TopicResource? EdiTopic { get; private set; }

    private ProcessManagerDatabaseManager DatabaseManager { get; }

    private AzuriteManager AzuriteManager { get; }

    private DurableTaskManager DurableTaskManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    public async Task InitializeAsync()
    {
        AzuriteManager.CleanupAzuriteStorage();
        AzuriteManager.StartAzurite();

        await DatabaseManager.CreateDatabaseAsync();

        // Process Manager Notify topic
        await ProcessManagerAppManager.StartAsync();

        var ediTopicBuilder = ServiceBusResourceProvider.BuildTopic("edi-topic");
        ExampleConsumerAppManager.EdiTopicResources.AddSubscriptionsToTopicBuilder(ediTopicBuilder);
        EdiTopic = await ediTopicBuilder.CreateAsync();

        // Process Manager Start topic
        await ExampleOrchestrationsAppManager.StartAsync(
            ExampleOrchestrationsAppManager.EdiTopicResources.CreateFromTopic(EdiTopic));

        await ExampleConsumerAppManager.StartAsync(
            ExampleOrchestrationsAppManager.ProcessManagerStartTopic,
            ProcessManagerAppManager.ProcessManagerNotifyTopic,
            ExampleOrchestrationsAppManager.ProcessManagerStartTopic, // TODO: Do we need to have specific "BRS-021 FMD" topics for the example apps?
            ProcessManagerAppManager.ProcessManagerNotifyTopic, // TODO: Do we need to have specific "BRS-021 FMD" topics for the example apps?
            ExampleConsumerAppManager.EdiTopicResources.CreateFromTopic(EdiTopic),
            processManagerApiUrl: ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.AbsoluteUri,
            orchestrationsApiUrl: ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.AbsoluteUri);

        // Create durable client when TaskHub has been created
        DurableClient = DurableTaskManager.CreateClient(taskHubName: TaskHubName);
    }

    public async Task DisposeAsync()
    {
        await ExampleConsumerAppManager.DisposeAsync();
        await ExampleOrchestrationsAppManager.DisposeAsync();
        await ProcessManagerAppManager.DisposeAsync();
        await DurableTaskManager.DisposeAsync();
        await DatabaseManager.DeleteDatabaseAsync();
        await ServiceBusResourceProvider.DisposeAsync();

        AzuriteManager.Dispose();
    }

    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        ExampleOrchestrationsAppManager.SetTestOutputHelper(testOutputHelper);
        ProcessManagerAppManager.SetTestOutputHelper(testOutputHelper);
        ExampleConsumerAppManager.SetTestOutputHelper(testOutputHelper);
    }
}
