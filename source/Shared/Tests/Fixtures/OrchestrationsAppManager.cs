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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus.Administration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.EventHub.ResourceProvider;
using Energinet.DataHub.Core.FunctionApp.TestCommon.FunctionAppHost;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using WireMock.Server;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

/// <summary>
/// Support testing Process Manager Orchestrations app and specifying configuration.
/// This allows us to use multiple apps and coordinate their configuration.
/// </summary>
public class OrchestrationsAppManager : IAsyncDisposable
{
    /// <summary>
    /// Durable Functions Task Hub Name
    /// See naming constraints: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-task-hubs?tabs=csharp#task-hub-names
    /// </summary>
    private readonly string _taskHubName;

    private readonly int _appPort;
    private readonly bool _manageDatabase;
    private readonly bool _manageAzurite;
    private readonly bool _manageEventHub;
    // TODO (ID-283)
    private readonly string? _environment;
    private readonly string _eventHubName;

    public OrchestrationsAppManager()
        : this(
            new ProcessManagerDatabaseManager("OrchestrationsTest"),
            new IntegrationTestConfiguration(),
            new AzuriteManager(useOAuth: true),
            taskHubName: "OrchestrationsTest01",
            appPort: 8002,
            wireMockServerPort: 8012,
            manageDatabase: true,
            manageAzurite: true,
            manageEventHub: true,
            // TODO (ID-283)
            environment: null,
            eventHubName: "eventhub_pm")
    {
    }

    public OrchestrationsAppManager(
        ProcessManagerDatabaseManager databaseManager,
        IntegrationTestConfiguration configuration,
        AzuriteManager azuriteManager,
        string taskHubName,
        int appPort,
        int wireMockServerPort,
        bool manageDatabase,
        bool manageAzurite,
        bool manageEventHub,
        // TODO (ID-283)
        string? environment,
        string eventHubName)
    {
        _taskHubName = string.IsNullOrWhiteSpace(taskHubName)
            ? throw new ArgumentException("Cannot be null or whitespace.", nameof(taskHubName))
            : taskHubName;
        _appPort = appPort;
        _manageDatabase = manageDatabase;
        _manageAzurite = manageAzurite;
        _manageEventHub = manageEventHub;
        // TODO (ID-283)
        _environment = environment;
        _eventHubName = eventHubName;

        DatabaseManager = databaseManager;
        TestLogger = new TestDiagnosticsLogger();

        IntegrationTestConfiguration = configuration;
        AzuriteManager = azuriteManager;
        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);

        EventHubResourceProvider = new EventHubResourceProvider(
            new TestDiagnosticsLogger(),
            IntegrationTestConfiguration.EventHubNamespaceName,
            IntegrationTestConfiguration.ResourceManagementSettings,
            IntegrationTestConfiguration.Credential);

        MockServer = WireMockServer.Start(port: wireMockServerPort);
    }

    public ProcessManagerDatabaseManager DatabaseManager { get; }

    public ITestDiagnosticsLogger TestLogger { get; }

    [NotNull]
    public FunctionAppHostManager? AppHostManager { get; private set; }

    public WireMockServer MockServer { get; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private AzuriteManager AzuriteManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    private EventHubResourceProvider EventHubResourceProvider { get; }

    /// <summary>
    /// Start the orchestration app
    /// </summary>
    /// <param name="orchestrationSubscriptions">The required Service Bus subscriptions. New subscriptions will be created if not provided.</param>
    public async Task StartAsync(
        ServiceBusResources? orchestrationSubscriptions = null)
    {
        if (_manageAzurite)
        {
            AzuriteManager.CleanupAzuriteStorage();
            AzuriteManager.StartAzurite();
        }

        if (_manageDatabase)
            await DatabaseManager.CreateDatabaseAsync();

        // TODO:  if (_manageEventHub)
        //await ResourceProvider.BuildEventHub(_measurementEventHubName).CreateAsync();

        if (orchestrationSubscriptions is null)
            orchestrationSubscriptions = await ServiceBusResources.Create(ServiceBusResourceProvider);

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings(
            "ProcessManager.Orchestrations",
            orchestrationSubscriptions);

        // Create and start host
        AppHostManager = new FunctionAppHostManager(appHostSettings, TestLogger);
        StartHost(AppHostManager);
    }

    public async ValueTask DisposeAsync()
    {
        AppHostManager.Dispose();

        if (_manageAzurite)
            AzuriteManager.Dispose();

        if (_manageDatabase)
            await DatabaseManager.DeleteDatabaseAsync();

        await ServiceBusResourceProvider.DisposeAsync();
        //TODO: await EventHubResourceProvider.DisposeAsync();
        MockServer.Dispose();
    }

    /// <summary>
    /// Use this method to attach <paramref name="testOutputHelper"/> to the host logging pipeline.
    /// While attached, any entries written to host log pipeline will also be logged to xUnit test output.
    /// It is important that it is only attached while a test is active. Hence, it should be attached in
    /// the test class constructor; and detached in the test class Dispose method (using 'null').
    /// </summary>
    /// <param name="testOutputHelper">If a xUnit test is active, this should be the instance of xUnit's <see cref="ITestOutputHelper"/>;
    /// otherwise it should be 'null'.</param>
    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        TestLogger.TestOutputHelper = testOutputHelper;
    }

    public void EnsureAppHostUsesMockedDatabricksApi(bool useMockServer = false)
    {
        AppHostManager.RestartHostIfChanges(new Dictionary<string, string>
        {
            {
                $"{DatabricksWorkspaceNames.Wholesale}__{nameof(DatabricksWorkspaceOptions.BaseUrl)}",
                useMockServer ? MockServer.Url! : IntegrationTestConfiguration.DatabricksSettings.WorkspaceUrl
            },
            {
                $"{DatabricksWorkspaceNames.Measurements}__{nameof(DatabricksWorkspaceOptions.BaseUrl)}",
                useMockServer ? MockServer.Url! : IntegrationTestConfiguration.DatabricksSettings.WorkspaceUrl
            },
        });
    }

    private static void StartHost(FunctionAppHostManager hostManager)
    {
        IEnumerable<string> hostStartupLog;

        try
        {
            hostManager.StartHost();
        }
        catch (Exception)
        {
            // Function App Host failed during startup.
            // Exception has already been logged by host manager.
            hostStartupLog = hostManager.GetHostLogSnapshot();

            if (Debugger.IsAttached)
                Debugger.Break();

            // Rethrow
            throw;
        }

        // Function App Host started.
        hostStartupLog = hostManager.GetHostLogSnapshot();
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private FunctionAppHostSettings CreateAppHostSettings(
        string csprojName,
        ServiceBusResources subscriptions)
    {
        var buildConfiguration = GetBuildConfiguration();

        var appHostSettings = new FunctionAppHostConfigurationBuilder()
            .CreateFunctionAppHostSettings();

        appHostSettings.FunctionApplicationPath = $"..\\..\\..\\..\\{csprojName}\\bin\\{buildConfiguration}\\net8.0";
        appHostSettings.Port = _appPort;

        // It seems the host + worker is not ready if we use the default startup log message, so we override it here
        appHostSettings.HostStartedEvent = "Host lock lease acquired";

        // TODO (ID-283): This is a temporary workaround to enable stubbing/mocking of external integrations.
        //  Please do not copy, duplicate, or otherwise get inspired by this code.
        //  The stubbing is achieved by changing the dependency injection
        //  to either use the real implementation or a stub based on the host environment.
        //  All relevant places in the code base for this workaround are marked with 'ID-283'.
        if (_environment is not null)
        {
            appHostSettings.ProcessEnvironmentVariables.Add(
                "ASPNETCORE_ENVIRONMENT",
                _environment);
            appHostSettings.ProcessEnvironmentVariables.Add(
                "DOTNET_ENVIRONMENT",
                _environment);
            appHostSettings.ProcessEnvironmentVariables.Add(
                "AZURE_FUNCTIONS_ENVIRONMENT",
                _environment);
        }

        appHostSettings.ProcessEnvironmentVariables.Add(
            "FUNCTIONS_WORKER_RUNTIME",
            "dotnet-isolated");
        appHostSettings.ProcessEnvironmentVariables.Add(
            "AzureWebJobsStorage",
            AzuriteManager.FullConnectionString);
        appHostSettings.ProcessEnvironmentVariables.Add(
            "APPLICATIONINSIGHTS_CONNECTION_STRING",
            IntegrationTestConfiguration.ApplicationInsightsConnectionString);
        // Make Orchestrator poll for updates every second (default is every 30 seconds) by overriding maxQueuePollingInterval
        // (ref: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-bindings?tabs=python-v2%2Cisolated-process%2C2x-durable-functions&pivots=programming-language-csharp#hostjson-settings)
        appHostSettings.ProcessEnvironmentVariables.Add(
            "AzureFunctionsJobHost__extensions__durableTask__storageProvider__maxQueuePollingInterval",
            "00:00:01");

        // ProcessManager
        // => Task Hub
        appHostSettings.ProcessEnvironmentVariables.Add(
            nameof(ProcessManagerTaskHubOptions.ProcessManagerStorageConnectionString),
            AzuriteManager.FullConnectionString);
        appHostSettings.ProcessEnvironmentVariables.Add(
            nameof(ProcessManagerTaskHubOptions.ProcessManagerTaskHubName),
            _taskHubName);
        // => Database
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerOptions.SectionName}__{nameof(ProcessManagerOptions.SqlDatabaseConnectionString)}",
            DatabaseManager.ConnectionString);

        // => Service Bus
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ServiceBusNamespaceOptions.SectionName}__{nameof(ServiceBusNamespaceOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace);

        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerTopicOptions.SectionName}__{nameof(ProcessManagerTopicOptions.TopicName)}",
            subscriptions.Brs026Subscription.TopicName);

        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerTopicOptions.SectionName}__{nameof(ProcessManagerTopicOptions.Brs021ForwardMeteredDataSubscriptionName)}",
            subscriptions.Brs021ForwardMeteredDataSubscription.SubscriptionName);

        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerTopicOptions.SectionName}__{nameof(ProcessManagerTopicOptions.Brs026SubscriptionName)}",
            subscriptions.Brs026Subscription.SubscriptionName);

        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerTopicOptions.SectionName}__{nameof(ProcessManagerTopicOptions.Brs028SubscriptionName)}",
            subscriptions.Brs028Subscription.SubscriptionName);

        // => Databricks workspaces
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{DatabricksWorkspaceNames.Wholesale}__{nameof(DatabricksWorkspaceOptions.BaseUrl)}",
            IntegrationTestConfiguration.DatabricksSettings.WorkspaceUrl);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{DatabricksWorkspaceNames.Wholesale}__{nameof(DatabricksWorkspaceOptions.Token)}",
            IntegrationTestConfiguration.DatabricksSettings.WorkspaceAccessToken);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{DatabricksWorkspaceNames.Measurements}__{nameof(DatabricksWorkspaceOptions.BaseUrl)}",
            IntegrationTestConfiguration.DatabricksSettings.WorkspaceUrl);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{DatabricksWorkspaceNames.Measurements}__{nameof(DatabricksWorkspaceOptions.Token)}",
            IntegrationTestConfiguration.DatabricksSettings.WorkspaceAccessToken);

        // Measurements Metered Data Event Hub
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{MeasurementsMeteredDataClientOptions.SectionName}__{nameof(MeasurementsMeteredDataClientOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.EventHubFullyQualifiedNamespace);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{MeasurementsMeteredDataClientOptions.SectionName}__{nameof(MeasurementsMeteredDataClientOptions.EventHubName)}",
            _eventHubName);

        return appHostSettings;
    }

    public class ServiceBusResources
    {
        private ServiceBusResources(
            TopicResource processManagerTopic,
            SubscriptionProperties brs021ForwardMeteredDataSubscription,
            SubscriptionProperties brs026Subscription,
            SubscriptionProperties brs028Subscription)
        {
            ProcessManagerTopic = processManagerTopic;
            Brs021ForwardMeteredDataSubscription = brs021ForwardMeteredDataSubscription;
            Brs026Subscription = brs026Subscription;
            Brs028Subscription = brs028Subscription;
        }

        public TopicResource ProcessManagerTopic { get; }

        public SubscriptionProperties Brs021ForwardMeteredDataSubscription { get; }

        public SubscriptionProperties Brs026Subscription { get; }

        public SubscriptionProperties Brs028Subscription { get; }

        public static async Task<ServiceBusResources> Create(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var topicResourceBuilder = serviceBusResourceProvider.BuildTopic("pm-topic");
            var brs021ForwardMeteredDataSubscriptionName = "brs-021-forward-metered-data-subscription";
            var brs026SubscriptionName = "brs-026-subscription";
            var brs028SubscriptionName = "brs-028-subscription";

            topicResourceBuilder
                .AddSubscription(brs021ForwardMeteredDataSubscriptionName)
                    .AddSubjectFilter("Brs_021_ForwardMeteredData")
                .AddSubscription(brs026SubscriptionName)
                    .AddSubjectFilter("Brs_026")
                .AddSubscription(brs028SubscriptionName)
                    .AddSubjectFilter("Brs_028");

            var processManagerTopic = await topicResourceBuilder.CreateAsync();
            var brs021ForwardMeteredDataSubscription = processManagerTopic.Subscriptions.Single(x => x.SubscriptionName.Equals(brs021ForwardMeteredDataSubscriptionName));
            var brs026Subscription = processManagerTopic.Subscriptions.Single(x => x.SubscriptionName.Equals(brs026SubscriptionName));
            var brs028Subscription = processManagerTopic.Subscriptions.Single(x => x.SubscriptionName.Equals(brs028SubscriptionName));

            return new ServiceBusResources(
                processManagerTopic,
                brs021ForwardMeteredDataSubscription,
                brs026Subscription,
                brs028Subscription);
        }
    }
}
