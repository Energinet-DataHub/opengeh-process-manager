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
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Tests.Fixtures;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Fixtures;

public class ObsoleteProcessManagerClientFixture : IAsyncLifetime
{
    private const string TaskHubName = "ObsoleteClientTest01";

    public ObsoleteProcessManagerClientFixture()
    {
        DatabaseManager = new ProcessManagerDatabaseManager("ObsoleteProcessManagerClientTests");
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
            manageDatabase: false,
            manageAzurite: false);

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
    }

    public IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    public OrchestrationsAppManager OrchestrationsAppManager { get; }

    public ProcessManagerAppManager ProcessManagerAppManager { get; }

    [NotNull]
    public IDurableClient? DurableClient { get; private set; }

    [NotNull]
    public TopicResource? ProcessManagerTopic { get; private set; }

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

        var brs026SubscriptionName = "brs-026-subscription";
        var brs021ForwardMeteredDataSubscriptionName = "brs-021-forward-metered-data-subscription";

        ProcessManagerTopic = await ServiceBusResourceProvider.BuildTopic("pm-topic")
            .AddSubscription(brs026SubscriptionName)
                .AddSubjectFilter("Brs_026")
            .AddSubscription(brs021ForwardMeteredDataSubscriptionName)
                .AddSubjectFilter("Brs_021_ForwardMeteredData")
            .CreateAsync();
        var brs026Subscription = ProcessManagerTopic.Subscriptions.Single(x => x.SubscriptionName.Equals(brs026SubscriptionName));
        var brs021ForwardMeteredDataSubscription = ProcessManagerTopic.Subscriptions.Single(x => x.SubscriptionName.Equals(brs021ForwardMeteredDataSubscriptionName));

        await OrchestrationsAppManager.StartAsync(brs026Subscription, brs021ForwardMeteredDataSubscription);
        await ProcessManagerAppManager.StartAsync();
    }

    public async Task DisposeAsync()
    {
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
