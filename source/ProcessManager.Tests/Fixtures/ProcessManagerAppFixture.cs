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

using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Tests.Fixtures;

/// <summary>
/// Support testing Process Manager Orchestrations app using default fixture configuration.
/// </summary>
public class ProcessManagerAppFixture : IAsyncLifetime
{
    private const string TaskHubName = "ApiOrchestrationsAppTest01";

    public ProcessManagerAppFixture()
    {
        DatabaseManager = new ProcessManagerDatabaseManager("ApiOrchestrationsAppTests");
        AzuriteManager = new AzuriteManager(useOAuth: true);

        IntegrationTestConfiguration = new IntegrationTestConfiguration();

        ExampleOrchestrationsAppManager = new ExampleOrchestrationsAppManager(
            DatabaseManager,
            IntegrationTestConfiguration,
            AzuriteManager,
            taskHubName: TaskHubName,
            appPort: 8201,
            manageDatabase: false,
            manageAzurite: false);

        ProcessManagerAppManager = new ProcessManagerAppManager(
            DatabaseManager,
            IntegrationTestConfiguration,
            AzuriteManager,
            taskHubName: TaskHubName,
            appPort: 8202,
            manageDatabase: false,
            manageAzurite: false);

        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            ExampleOrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    public ExampleOrchestrationsAppManager ExampleOrchestrationsAppManager { get; }

    public ProcessManagerAppManager ProcessManagerAppManager { get; }

    private ProcessManagerDatabaseManager DatabaseManager { get; }

    private AzuriteManager AzuriteManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    public async Task InitializeAsync()
    {
        AzuriteManager.CleanupAzuriteStorage();
        AzuriteManager.StartAzurite();

        await DatabaseManager.CreateDatabaseAsync();

        var processManagerTopicBuilder = ServiceBusResourceProvider.BuildTopic("pm-topic");

        ExampleOrchestrationsAppManager.ProcessManagerTopicResources.AddSubscriptionsToTopicBuilder(processManagerTopicBuilder);
        ProcessManagerAppManager.ProcessManagerTopicResources.AddSubscriptionsToTopicBuilder(processManagerTopicBuilder);

        var processManagerTopic = await processManagerTopicBuilder.CreateAsync();

        await ExampleOrchestrationsAppManager.StartAsync(
            ExampleOrchestrationsAppManager.ProcessManagerTopicResources.CreateFromTopic(processManagerTopic));

        await ProcessManagerAppManager.StartAsync(
            ProcessManagerAppManager.ProcessManagerTopicResources.CreateFromTopic(processManagerTopic));
    }

    public async Task DisposeAsync()
    {
        await ExampleOrchestrationsAppManager.DisposeAsync();
        await ProcessManagerAppManager.DisposeAsync();

        await DatabaseManager.DeleteDatabaseAsync();

        await ServiceBusResourceProvider.DisposeAsync();

        AzuriteManager.Dispose();
    }

    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        ExampleOrchestrationsAppManager.SetTestOutputHelper(testOutputHelper);
        ProcessManagerAppManager.SetTestOutputHelper(testOutputHelper);
    }
}
