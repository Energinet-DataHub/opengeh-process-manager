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
using Energinet.DataHub.ElectricityMarket.Integration.Options;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
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

        WholesaleDatabaseManager = new WholesaleDatabaseManager("Wholesale");
    }

    public ProcessManagerDatabaseManager DatabaseManager { get; }

    public ITestDiagnosticsLogger TestLogger { get; }

    [NotNull]
    public FunctionAppHostManager? AppHostManager { get; private set; }

    [NotNull]
    public TopicResource? ProcessManagerStartTopic { get; private set; }

    [NotNull]
    public string? MeasurementEventHubName { get; private set; }

    [NotNull]
    public string? ProcessManagerEventhubName { get; private set; }

    public WireMockServer MockServer { get; }

    public WholesaleDatabaseManager WholesaleDatabaseManager { get; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private AzuriteManager AzuriteManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    private EventHubResourceProvider EventHubResourceProvider { get; }

    /// <summary>
    /// Start the orchestration app
    /// </summary>
    /// <param name="ediTopicResources">The required EDI topic resources. New resources will be created if not provided.</param>
    /// <param name="brs21TopicResource">The required Brs 21 topic resources. New resources will be created if not provided.</param>
    /// <param name="integrationEventTopicResources">The required shared integration event topic resources. New resources will be created if not provided.</param>
    public async Task StartAsync(
        EdiTopicResources? ediTopicResources,
        Brs21TopicResources? brs21TopicResource,
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

        // Start topic
        var startTopicResources = await ProcessManagerTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        ProcessManagerStartTopic = startTopicResources.StartTopic;
        // EDI topic
        ediTopicResources ??= await EdiTopicResources.CreateNew(ServiceBusResourceProvider);
        brs21TopicResource ??= await Brs21TopicResources.CreateNew(ServiceBusResourceProvider);

        // Integration event topic
        integrationEventTopicResources ??= await IntegrationEventTopicResources.CreateNew(ServiceBusResourceProvider);

        await WholesaleDatabaseManager.CreateDatabaseAsync();

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings(
            "ProcessManager.Orchestrations",
            startTopicResources,
            ediTopicResources,
            brs21TopicResource,
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

        await WholesaleDatabaseManager.DeleteDatabaseAsync();
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
        ProcessManagerTopicResources processManagerTopicResources,
        EdiTopicResources ediTopicResources,
        Brs21TopicResources brs21TopicResources,
        IntegrationEventTopicResources integrationEventTopicResources,
        EventHubResource eventHubResource,
        EventHubResource processManagerEventhubResource)
    {
        var buildConfiguration = GetBuildConfiguration();

        var appHostSettings = new FunctionAppHostConfigurationBuilder()
            .CreateFunctionAppHostSettings();

        appHostSettings.FunctionApplicationPath = $"..\\..\\..\\..\\{csprojName}\\bin\\{buildConfiguration}\\net8.0";
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
            processManagerTopicResources.StartTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.Brs026SubscriptionName)}",
            processManagerTopicResources.Brs026Subscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.Brs028SubscriptionName)}",
            processManagerTopicResources.Brs028Subscription.SubscriptionName);

        // brs 21 topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.StartTopicName)}",
            brs21TopicResources.Brs21StartTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.NotifyTopicName)}",
            brs21TopicResources.Brs21NotifyTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.StartSubscriptionName)}",
            brs21TopicResources.StartSubscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.NotifySubscriptionName)}",
            brs21TopicResources.NotifySubscription.SubscriptionName);

        // => EDI topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{EdiTopicOptions.SectionName}__{nameof(EdiTopicOptions.Name)}",
            ediTopicResources.EdiTopic.Name);

        // => Shared integration event topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{IntegrationEventTopicOptions.SectionName}__{nameof(IntegrationEventTopicOptions.Name)}",
            integrationEventTopicResources.SharedTopic.Name);

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

        // => BRS 023 027
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_023_027_V1.SectionName}__{nameof(OrchestrationOptions_Brs_023_027_V1.CalculationJobStatusPollingIntervalInSeconds)}",
            "3");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_023_027_V1.SectionName}__{nameof(OrchestrationOptions_Brs_023_027_V1.CalculationJobStatusExpiryTimeInSeconds)}",
            "20");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_023_027_V1.SectionName}__{nameof(OrchestrationOptions_Brs_023_027_V1.MessagesEnqueuingExpiryTimeInSeconds)}",
            "20");

        // Process Manager Event Hub
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerEventHubOptions.SectionName}__{nameof(ProcessManagerEventHubOptions.EventHubName)}",
            processManagerEventhubResource.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerEventHubOptions.SectionName}__{nameof(ProcessManagerEventHubOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.EventHubFullyQualifiedNamespace);

        // Measurements Metered Data Event Hub
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{MeasurementsMeteredDataClientOptions.SectionName}__{nameof(MeasurementsMeteredDataClientOptions.EventHubName)}",
            MeasurementEventHubName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{MeasurementsMeteredDataClientOptions.SectionName}__{nameof(MeasurementsMeteredDataClientOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.EventHubFullyQualifiedNamespace);

        // Electric Market client
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(ElectricityMarketClientOptions)}__{nameof(ElectricityMarketClientOptions.BaseUrl)}",
            MockServer.Url!);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(ElectricityMarketClientOptions)}__{nameof(ElectricityMarketClientOptions.ApplicationIdUri)}",
            AuthenticationOptionsForTests.ApplicationIdUri);

        // => BRS-026
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_026_V1.SectionName}__{nameof(OrchestrationOptions_Brs_026_V1.EnqueueActorMessagesTimeout)}",
            TimeSpan.FromSeconds(60).ToString());

        // => BRS-028
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_028_V1.SectionName}__{nameof(OrchestrationOptions_Brs_028_V1.EnqueueActorMessagesTimeout)}",
            TimeSpan.FromSeconds(60).ToString());

        // => Wholesale migration (database)
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{WholesaleDatabaseOptions.SectionName}__{nameof(WholesaleDatabaseOptions.SqlDatabaseConnectionString)}",
            WholesaleDatabaseManager.ConnectionString);

        return appHostSettings;
    }

    /// <summary>
    /// Process Manager topic and subscription resources used by the Orchestrations app.
    /// </summary>
    public record ProcessManagerTopicResources(
        TopicResource StartTopic,
        SubscriptionProperties Brs021ForwardMeteredDataSubscription,
        SubscriptionProperties Brs023027Subscription,
        SubscriptionProperties Brs026Subscription,
        SubscriptionProperties Brs028Subscription)
    {
        private const string Brs021ForwardMeteredDataSubscriptionName = "brs-021-forward-metered-data-subscription";
        private const string Brs023027SubscriptionName = "brs-023-027-subscription";
        private const string Brs026SubscriptionName = "brs-026-subscription";
        private const string Brs028SubscriptionName = "brs-028-subscription";

        public static async Task<ProcessManagerTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var processManagerTopicBuilder = serviceBusResourceProvider.BuildTopic("pm-start-topic");
            AddOrchestrationsAppSubscriptions(processManagerTopicBuilder);

            var processManagerTopic = await processManagerTopicBuilder.CreateAsync();

            return CreateFromTopic(processManagerTopic);
        }

        /// <summary>
        /// Add the subscriptions used by the Orchestrations app to the topic builder.
        /// </summary>
        public static TopicResourceBuilder AddOrchestrationsAppSubscriptions(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(Brs021ForwardMeteredDataSubscriptionName)
                    .AddSubjectFilter(Brs_021_ForwardedMeteredData.Name)
                .AddSubscription(Brs023027SubscriptionName)
                    .AddSubjectFilter(Brs_023_027.Name)
                .AddSubscription(Brs026SubscriptionName)
                    .AddSubjectFilter(Brs_026.Name)
                .AddSubscription(Brs028SubscriptionName)
                    .AddSubjectFilter(Brs_028.Name);

            return builder;
        }

        /// <summary>
        /// Get the <see cref="OrchestrationsAppManager.ProcessManagerTopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// This requires the Orchestration subscriptions to be created on the topic, using <see cref="AddOrchestrationsAppSubscriptions"/>.
        /// </remarks>
        /// </summary>
        public static ProcessManagerTopicResources CreateFromTopic(TopicResource topic)
        {
            var brs021ForwardMeteredDataSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs021ForwardMeteredDataSubscriptionName));

            var brs023027Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs023027SubscriptionName));

            var brs026Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs026SubscriptionName));

            var brs028Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs028SubscriptionName));

            return new ProcessManagerTopicResources(
                StartTopic: topic,
                Brs021ForwardMeteredDataSubscription: brs021ForwardMeteredDataSubscription,
                Brs023027Subscription: brs023027Subscription,
                Brs026Subscription: brs026Subscription,
                Brs028Subscription: brs028Subscription);
        }
    }

    /// <summary>
    /// EDI topic resources.
    /// </summary>
    public record EdiTopicResources(
        TopicResource EdiTopic,
        SubscriptionProperties EnqueueBrs021ForwardMeteredDataSubscription,
        SubscriptionProperties EnqueueBrs023027Subscription,
        SubscriptionProperties EnqueueBrs026Subscription,
        SubscriptionProperties EnqueueBrs028Subscription)
    {
        private const string EnqueueBrs021ForwardMeteredDataSubscriptionName = "enqueue-brs-021-forwardmetereddata-subscription";
        private const string EnqueueBrs023027SubscriptionName = "enqueue-brs-023-027-subscription";
        private const string EnqueueBrs026SubscriptionName = "enqueue-brs-026-subscription";
        private const string EnqueueBrs028SubscriptionName = "enqueue-brs-028-subscription";

        public static async Task<EdiTopicResources> CreateNew(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var ediTopicBuilder = serviceBusResourceProvider.BuildTopic("edi-topic");
            AddSubscriptionsToTopicBuilder(ediTopicBuilder);

            var ediTopic = await ediTopicBuilder.CreateAsync();

            return CreateFromTopic(ediTopic);
        }

        /// <summary>
        /// Add EDI subscriptions to the EDI topic.
        /// </summary>
        public static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(EnqueueBrs021ForwardMeteredDataSubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(OrchestrationDescriptionBuilderV1.UniqueName))
                .AddSubscription(EnqueueBrs023027SubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_023_027.V1))
                .AddSubscription(EnqueueBrs026SubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_026.V1))
                .AddSubscription(EnqueueBrs028SubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_028.V1));

            return builder;
        }

        /// <summary>
        /// Get the <see cref="OrchestrationsAppManager.EdiTopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// This requires the Orchestration subscriptions to be created on the topic, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        public static EdiTopicResources CreateFromTopic(TopicResource topic)
        {
            var enqueueBrs021ForwardMeteredDataSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(EnqueueBrs021ForwardMeteredDataSubscriptionName));
            var enqueueBrs023027Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(EnqueueBrs023027SubscriptionName));
            var enqueueBrs026Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(EnqueueBrs026SubscriptionName));
            var enqueueBrs028Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(EnqueueBrs028SubscriptionName));

            return new EdiTopicResources(
                EdiTopic: topic,
                EnqueueBrs021ForwardMeteredDataSubscription: enqueueBrs021ForwardMeteredDataSubscription,
                EnqueueBrs023027Subscription: enqueueBrs023027Subscription,
                EnqueueBrs026Subscription: enqueueBrs026Subscription,
                EnqueueBrs028Subscription: enqueueBrs028Subscription);
        }
    }

    /// <summary>
    /// Brs21 topic resources.
    /// </summary>
    public record Brs21TopicResources(
        TopicResource Brs21StartTopic,
        SubscriptionProperties StartSubscription,
        TopicResource Brs21NotifyTopic,
        SubscriptionProperties NotifySubscription)
    {
        private const string StartSubscriptionName = "brs-021-start-subscription";
        private const string NotifySubscriptionName = "brs-021-notify-subscription";

        public static async Task<Brs21TopicResources> CreateNew(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var brs21StartTopicBuilder = serviceBusResourceProvider.BuildTopic("brs21-start-topic");
            AddSubscriptionsToTopicBuilder(brs21StartTopicBuilder, StartSubscriptionName);
            var brs21StartTopic = await brs21StartTopicBuilder.CreateAsync();

            var brs21NotifyTopicBuilder = serviceBusResourceProvider.BuildTopic("brs21-notify-topic");
            AddSubscriptionsToTopicBuilder(brs21NotifyTopicBuilder, NotifySubscriptionName);
            var brs21NotifyTopic = await brs21NotifyTopicBuilder.CreateAsync();

            return new Brs21TopicResources(
                Brs21StartTopic: brs21StartTopic,
                StartSubscription: GetSubscription(brs21StartTopic, StartSubscriptionName),
                Brs21NotifyTopic: brs21NotifyTopic,
                NotifySubscription: GetSubscription(brs21NotifyTopic, NotifySubscriptionName));
        }

        /// <summary>
        /// Add Brs21 subscriptions to the Brs21 topic.
        /// </summary>
        public static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder, string subscriptionName)
        {
            builder
                .AddSubscription(subscriptionName);

            return builder;
        }

        /// <summary>
        /// Get the <see cref="OrchestrationsAppManager.Brs21TopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// This requires the Orchestration subscriptions to be created on the topic, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        public static SubscriptionProperties GetSubscription(TopicResource topic, string subscriptionName)
        {
            return topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(subscriptionName));
        }
    }

    public record IntegrationEventTopicResources(
        TopicResource SharedTopic,
        SubscriptionProperties Subscription)
    {
        private const string SubscriptionName = "integration-event-subscription";

        public static async Task<IntegrationEventTopicResources> CreateNew(
            ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var integrationEventTopicBuilder = serviceBusResourceProvider.BuildTopic("integration-event-topic");
            AddSubscriptionsToTopicBuilder(integrationEventTopicBuilder);

            var integrationEventTopic = await integrationEventTopicBuilder.CreateAsync();

            return CreateFromTopic(integrationEventTopic);
        }

        /// <summary>
        /// Add integration event subscription to the integration event topic.
        /// </summary>
        public static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(SubscriptionName);

            return builder;
        }

        /// <summary>
        /// Get the <see cref="IntegrationEventTopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// This requires the Orchestration subscriptions to be created on the topic, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        public static IntegrationEventTopicResources CreateFromTopic(TopicResource topic)
        {
            var integrationEventSubscriptionName = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(SubscriptionName));

            return new IntegrationEventTopicResources(
                SharedTopic: topic,
                Subscription: integrationEventSubscriptionName);
        }
    }
}
