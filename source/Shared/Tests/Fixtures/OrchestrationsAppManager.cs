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
using Energinet.DataHub.Core.Databricks.SqlStatementExecution;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.EventHub.ResourceProvider;
using Energinet.DataHub.Core.FunctionApp.TestCommon.FunctionAppHost;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ElectricityMarket.Integration.Options;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeasurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.CalculatedMeasurements.V1.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Options;
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
    private readonly string _measurementEventHubName;

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
            measurementEventHubName: "eventhub_measurement")
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
        string measurementEventHubName)
    {
        _taskHubName = string.IsNullOrWhiteSpace(taskHubName)
            ? throw new ArgumentException("Cannot be null or whitespace.", nameof(taskHubName))
            : taskHubName;
        _appPort = appPort;
        _manageDatabase = manageDatabase;
        _manageAzurite = manageAzurite;
        _measurementEventHubName = measurementEventHubName;

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

    [NotNull]
    public TopicResource? ProcessManagerStartTopic { get; private set; }

    [NotNull]
    public TopicResource? Brs021ForwardMeteredDataStartTopic { get; private set; }

    [NotNull]
    public TopicResource? Brs021ForwardMeteredDataNotifyTopic { get; private set; }

    [NotNull]
    public string? MeasurementEventHubName { get; private set; }

    [NotNull]
    public string? ProcessManagerEventhubName { get; private set; }

    public WireMockServer MockServer { get; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private AzuriteManager AzuriteManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    private EventHubResourceProvider EventHubResourceProvider { get; }

    /// <summary>
    /// Start the Orchestrations app.
    /// </summary>
    /// <param name="ediEnqueueTopicResources">EDI enqueue actor messages topic resources used by the app.
    /// Will be created if not provided.</param>
    /// <param name="integrationEventTopicResources">Shared integration event topic resources used by the app.
    /// Will be created if not provided.</param>
    public async Task StartAsync(
        EdiEnqueueTopicResources? ediEnqueueTopicResources,
        IntegrationEventTopicResources? integrationEventTopicResources)
    {
        if (_manageAzurite)
        {
            AzuriteManager.CleanupAzuriteStorage();
            AzuriteManager.StartAzurite();
        }

        if (_manageDatabase)
            await DatabaseManager.CreateDatabaseAsync();

        var measurementEventHubResource = await EventHubResourceProvider.BuildEventHub(_measurementEventHubName).CreateAsync();
        MeasurementEventHubName = measurementEventHubResource.Name;

        var processManagerEventhubResource = await EventHubResourceProvider.BuildEventHub("process-manager-event-hub").CreateAsync();
        ProcessManagerEventhubName = processManagerEventhubResource.Name;

        // Creates Process Manager default Start topics and subscriptions
        var startTopicResources = await ProcessManagerStartTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        ProcessManagerStartTopic = startTopicResources.StartTopic;

        // Creates BRS-021 Forward Metered Data Start/Notify topics and subscriptions
        var brs21fmdTopicResource = await Brs021ForwardMeteredDataTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        Brs021ForwardMeteredDataStartTopic = brs21fmdTopicResource.StartTopic;
        Brs021ForwardMeteredDataNotifyTopic = brs21fmdTopicResource.NotifyTopic;

        // Creates EDI enqueue actor messages topic and subscriptions
        ediEnqueueTopicResources ??= await EdiEnqueueTopicResources.CreateNewAsync(ServiceBusResourceProvider);

        // Creates Integration event topic and subscriptions
        integrationEventTopicResources ??= await IntegrationEventTopicResources.CreateNewAsync(ServiceBusResourceProvider);

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings(
            "ProcessManager.Orchestrations",
            startTopicResources,
            ediEnqueueTopicResources,
            brs21fmdTopicResource,
            integrationEventTopicResources,
            measurementEventHubResource,
            processManagerEventhubResource);

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
        await EventHubResourceProvider.DisposeAsync();
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
            {
                $"{nameof(DatabricksSqlStatementOptions.WorkspaceUrl)}",
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
        ProcessManagerStartTopicResources startTopicResources,
        EdiEnqueueTopicResources ediEnqueueTopicResources,
        Brs021ForwardMeteredDataTopicResources brs21fmdTopicResources,
        IntegrationEventTopicResources integrationEventTopicResources,
        EventHubResource eventHubResource,
        EventHubResource processManagerEventhubResource)
    {
        var buildConfiguration = GetBuildConfiguration();

        var appHostSettings = new FunctionAppHostConfigurationBuilder()
            .CreateFunctionAppHostSettings();

        appHostSettings.FunctionApplicationPath = $"..\\..\\..\\..\\{csprojName}\\bin\\{buildConfiguration}\\net9.0";
        appHostSettings.Port = _appPort;

        // It seems the host + worker is not ready if we use the default startup log message, so we override it here
        appHostSettings.HostStartedEvent = "Host lock lease acquired";

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

        // Logging
        appHostSettings.ProcessEnvironmentVariables.Add(
            "Logging__LogLevel__Default",
            "Information");
        // => Disable extensive logging from EF Core
        appHostSettings.ProcessEnvironmentVariables.Add(
            "Logging__LogLevel__Microsoft.EntityFrameworkCore",
            "Warning");
        // => Disable extensive logging when using Azure Storage
        appHostSettings.ProcessEnvironmentVariables.Add(
            "Logging__LogLevel__Azure.Core",
            "Error");

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
        // => Authentication
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{AuthenticationOptions.SectionName}__{nameof(AuthenticationOptions.ApplicationIdUri)}",
            AuthenticationOptionsForTests.ApplicationIdUri);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{AuthenticationOptions.SectionName}__{nameof(AuthenticationOptions.Issuer)}",
            AuthenticationOptionsForTests.Issuer);

        // => Service Bus
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ServiceBusNamespaceOptions.SectionName}__{nameof(ServiceBusNamespaceOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace);

        // => Process Manager Start topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.TopicName)}",
            startTopicResources.StartTopic.Name);
        // => Process Manager Start topic -> subscriptions
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.Brs026SubscriptionName)}",
            startTopicResources.Brs026Subscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.Brs028SubscriptionName)}",
            startTopicResources.Brs028Subscription.SubscriptionName);

        // => BRS-021 Forward Metered Data topics and subscriptions
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.StartTopicName)}",
            brs21fmdTopicResources.StartTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.NotifyTopicName)}",
            brs21fmdTopicResources.NotifyTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.StartSubscriptionName)}",
            brs21fmdTopicResources.StartSubscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.NotifySubscriptionName)}",
            brs21fmdTopicResources.NotifySubscription.SubscriptionName);

        // => Edi enqueue topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{EdiTopicOptions.SectionName}__{nameof(EdiTopicOptions.Name)}",
            ediEnqueueTopicResources.EnqueueTopic.Name);

        // => Shared integration event topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{IntegrationEventTopicOptions.SectionName}__{nameof(IntegrationEventTopicOptions.Name)}",
            integrationEventTopicResources.SharedTopic.Name);

        // => Databricks workspaces
        // Databricks jobs API for Wholesale
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{DatabricksWorkspaceNames.Wholesale}__{nameof(DatabricksWorkspaceOptions.BaseUrl)}",
            MockServer.Url!); // Default to use MockServer
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{DatabricksWorkspaceNames.Wholesale}__{nameof(DatabricksWorkspaceOptions.Token)}",
            IntegrationTestConfiguration.DatabricksSettings.WorkspaceAccessToken);

        // Databricks jobs API for Measurement
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{DatabricksWorkspaceNames.Measurements}__{nameof(DatabricksWorkspaceOptions.BaseUrl)}",
            MockServer.Url!); // Default to use MockServer
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{DatabricksWorkspaceNames.Measurements}__{nameof(DatabricksWorkspaceOptions.Token)}",
            IntegrationTestConfiguration.DatabricksSettings.WorkspaceAccessToken);

        // Databricks SQL statement API
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(DatabricksSqlStatementOptions.WorkspaceUrl)}",
            MockServer.Url!); // Default to use MockServer
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(DatabricksSqlStatementOptions.WarehouseId)}",
            IntegrationTestConfiguration.DatabricksSettings.WarehouseId);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(DatabricksSqlStatementOptions.WorkspaceToken)}",
            IntegrationTestConfiguration.DatabricksSettings.WorkspaceAccessToken);

        // => BRS 023 027 options
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_023_027_V1.SectionName}__{nameof(OrchestrationOptions_Brs_023_027_V1.CalculationJobStatusPollingIntervalInSeconds)}",
            "3");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_023_027_V1.SectionName}__{nameof(OrchestrationOptions_Brs_023_027_V1.CalculationJobStatusExpiryTimeInSeconds)}",
            "20");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_023_027_V1.SectionName}__{nameof(OrchestrationOptions_Brs_023_027_V1.MessagesEnqueuingExpiryTimeInSeconds)}",
            "20");

        // => BRS 021 Electrical Heating options
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1.SectionName}__{nameof(OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1.CalculationJobStatusPollingIntervalInSeconds)}",
            "3");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1.SectionName}__{nameof(OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1.CalculationJobStatusExpiryTimeInSeconds)}",
            "20");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1.SectionName}__{nameof(OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1.MessagesEnqueuingExpiryTimeInSeconds)}",
            "20");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{QueryOptionsSectionNames.CalculatedMeasurementsQuery}__{nameof(DatabricksQueryOptions.CatalogName)}",
            "hive_metastore");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{QueryOptionsSectionNames.CalculatedMeasurementsQuery}__{nameof(DatabricksQueryOptions.DatabaseName)}",
            "measurements_calculated");

        // Process Manager Event Hub
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerEventHubOptions.SectionName}__{nameof(ProcessManagerEventHubOptions.EventHubName)}",
            processManagerEventhubResource.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerEventHubOptions.SectionName}__{nameof(ProcessManagerEventHubOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.EventHubFullyQualifiedNamespace);

        // Measurements Event Hub
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{MeasurementsClientOptions.SectionName}__{nameof(MeasurementsClientOptions.EventHubName)}",
            MeasurementEventHubName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{MeasurementsClientOptions.SectionName}__{nameof(MeasurementsClientOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.EventHubFullyQualifiedNamespace);

        // Electric Market client
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(ElectricityMarketClientOptions)}__{nameof(ElectricityMarketClientOptions.BaseUrl)}",
            MockServer.Url!);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(ElectricityMarketClientOptions)}__{nameof(ElectricityMarketClientOptions.ApplicationIdUri)}",
            AuthenticationOptionsForTests.ApplicationIdUri);

        // => BRS-026 options
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_026_V1.SectionName}__{nameof(OrchestrationOptions_Brs_026_V1.EnqueueActorMessagesTimeout)}",
            TimeSpan.FromSeconds(60).ToString());

        // => BRS-028 options
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_028_V1.SectionName}__{nameof(OrchestrationOptions_Brs_028_V1.EnqueueActorMessagesTimeout)}",
            TimeSpan.FromSeconds(60).ToString());

        return appHostSettings;
    }

    /// <summary>
    /// EDI enqueue actor messages topic and subscription resources used by the Orchestrations app.
    /// </summary>
    public record EdiEnqueueTopicResources(
        TopicResource EnqueueTopic,
        SubscriptionProperties Brs021ForwardMeteredDataSubscription,
        SubscriptionProperties Brs023027Subscription,
        SubscriptionProperties Brs026Subscription,
        SubscriptionProperties Brs028Subscription)
    {
        private const string Brs021ForwardMeteredDataSubscriptionName = "brs-021-forwardmetereddata";
        private const string Brs023027SubscriptionName = "brs-023-027";
        private const string Brs026SubscriptionName = "brs-026";
        private const string Brs028SubscriptionName = "brs-028";

        public static async Task<EdiEnqueueTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var topicBuilder = serviceBusResourceProvider.BuildTopic("edi-topic");
            AddSubscriptionsToTopicBuilder(topicBuilder);

            var topic = await topicBuilder.CreateAsync();
            return CreateFromTopic(topic);
        }

        /// <summary>
        /// Add the subscriptions used by the Orchestrations app to the topic builder.
        /// </summary>
        public static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(Brs021ForwardMeteredDataSubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(OrchestrationDescriptionBuilder.UniqueName))
                .AddSubscription(Brs023027SubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_023_027.V1))
                .AddSubscription(Brs026SubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_026.V1))
                .AddSubscription(Brs028SubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_028.V1));

            return builder;
        }

        /// <summary>
        /// Get the <see cref="EdiEnqueueTopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// Subscriptions must be created on the topic beforehand, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        public static EdiEnqueueTopicResources CreateFromTopic(TopicResource topic)
        {
            var enqueueBrs021ForwardMeteredDataSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs021ForwardMeteredDataSubscriptionName));
            var enqueueBrs023027Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs023027SubscriptionName));
            var enqueueBrs026Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs026SubscriptionName));
            var enqueueBrs028Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs028SubscriptionName));

            return new EdiEnqueueTopicResources(
                EnqueueTopic: topic,
                Brs021ForwardMeteredDataSubscription: enqueueBrs021ForwardMeteredDataSubscription,
                Brs023027Subscription: enqueueBrs023027Subscription,
                Brs026Subscription: enqueueBrs026Subscription,
                Brs028Subscription: enqueueBrs028Subscription);
        }
    }

    public record IntegrationEventTopicResources(
        TopicResource SharedTopic,
        SubscriptionProperties Subscription)
    {
        public static async Task<IntegrationEventTopicResources> CreateNewAsync(
            ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var topic = await serviceBusResourceProvider
                .BuildTopic("integration-event-topic")
                .AddSubscription("integration-event-subscription")
                .CreateAsync();

            return new IntegrationEventTopicResources(
                SharedTopic: topic,
                Subscription: topic.Subscriptions.Single());
        }
    }

    /// <summary>
    /// Process Manager default start topic and subscription resources used by the Orchestrations app.
    /// </summary>
    private record ProcessManagerStartTopicResources(
        TopicResource StartTopic,
        SubscriptionProperties Brs021ForwardMeteredDataSubscription,
        SubscriptionProperties Brs023027Subscription,
        SubscriptionProperties Brs026Subscription,
        SubscriptionProperties Brs028Subscription)
    {
        private const string Brs021ForwardMeteredDataSubscriptionName = "brs-021-forward-metered-data";
        private const string Brs023027SubscriptionName = "brs-023-027";
        private const string Brs026SubscriptionName = "brs-026";
        private const string Brs028SubscriptionName = "brs-028";

        internal static async Task<ProcessManagerStartTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var topicBuilder = serviceBusResourceProvider.BuildTopic("pm-start-topic");
            AddSubscriptionsToTopicBuilder(topicBuilder);

            var topic = await topicBuilder.CreateAsync();
            return CreateFromTopic(topic);
        }

        /// <summary>
        /// Add the subscriptions used by the Orchestrations app to the topic builder.
        /// </summary>
        private static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(Brs021ForwardMeteredDataSubscriptionName)
                    .AddSubjectFilter(Brs_021_ForwardMeasurements.Name)
                .AddSubscription(Brs023027SubscriptionName)
                    .AddSubjectFilter(Brs_023_027.Name)
                .AddSubscription(Brs026SubscriptionName)
                    .AddSubjectFilter(Brs_026.Name)
                .AddSubscription(Brs028SubscriptionName)
                    .AddSubjectFilter(Brs_028.Name);

            return builder;
        }

        /// <summary>
        /// Get the <see cref="ProcessManagerStartTopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// Subscriptions must be created on the topic beforehand, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        private static ProcessManagerStartTopicResources CreateFromTopic(TopicResource topic)
        {
            var brs021ForwardMeteredDataSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs021ForwardMeteredDataSubscriptionName));

            var brs023027Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs023027SubscriptionName));

            var brs026Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs026SubscriptionName));

            var brs028Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs028SubscriptionName));

            return new ProcessManagerStartTopicResources(
                StartTopic: topic,
                Brs021ForwardMeteredDataSubscription: brs021ForwardMeteredDataSubscription,
                Brs023027Subscription: brs023027Subscription,
                Brs026Subscription: brs026Subscription,
                Brs028Subscription: brs028Subscription);
        }
    }

    /// <summary>
    /// BRS-021 Forward Metered Data start + notify topic and subscription resources used by the Orchestrations app.
    /// </summary>
    private record Brs021ForwardMeteredDataTopicResources(
        TopicResource StartTopic,
        SubscriptionProperties StartSubscription,
        TopicResource NotifyTopic,
        SubscriptionProperties NotifySubscription)
    {
        private const string StartSubscriptionName = "brs-021-forwardmetereddata-start";
        private const string NotifySubscriptionName = "brs-021-forwardmetereddata-notify";

        internal static async Task<Brs021ForwardMeteredDataTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var startTopic = await serviceBusResourceProvider
                .BuildTopic("brs021-start-topic")
                .AddSubscription(StartSubscriptionName)
                .CreateAsync();

            var notifyTopic = await serviceBusResourceProvider
                .BuildTopic("brs021-notify-topic")
                .AddSubscription(NotifySubscriptionName)
                .CreateAsync();

            return new Brs021ForwardMeteredDataTopicResources(
                StartTopic: startTopic,
                StartSubscription: startTopic.Subscriptions.Single(),
                NotifyTopic: notifyTopic,
                NotifySubscription: notifyTopic.Subscriptions.Single());
        }
    }
}
