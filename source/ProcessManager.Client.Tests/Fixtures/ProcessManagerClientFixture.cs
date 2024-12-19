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
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Tests.Fixtures;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Fixtures;

/// <summary>
/// Support testing the Process Manager Clients by coordinating the startup
/// of the dependent applications Example.Orchestrations and ProcessManager (Api).
/// </summary>
public class ProcessManagerClientFixture : IAsyncLifetime
{
    private const string TaskHubName = "ClientTest01";

    public ProcessManagerClientFixture()
    {
        DatabaseManager = new ProcessManagerDatabaseManager("ProcessManagerClientTests");
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
    }

    public IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    public ExampleOrchestrationsAppManager ExampleOrchestrationsAppManager { get; }

    public ProcessManagerAppManager ProcessManagerAppManager { get; }

    private ProcessManagerDatabaseManager DatabaseManager { get; }

    private AzuriteManager AzuriteManager { get; }

    public async Task InitializeAsync()
    {
        AzuriteManager.CleanupAzuriteStorage();
        AzuriteManager.StartAzurite();

        await DatabaseManager.CreateDatabaseAsync();

        await ExampleOrchestrationsAppManager.StartAsync();
        await ProcessManagerAppManager.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await ExampleOrchestrationsAppManager.DisposeAsync();
        await ProcessManagerAppManager.DisposeAsync();

        await DatabaseManager.DeleteDatabaseAsync();

        AzuriteManager.Dispose();
    }

    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        ExampleOrchestrationsAppManager.SetTestOutputHelper(testOutputHelper);
        ProcessManagerAppManager.SetTestOutputHelper(testOutputHelper);
    }
}
