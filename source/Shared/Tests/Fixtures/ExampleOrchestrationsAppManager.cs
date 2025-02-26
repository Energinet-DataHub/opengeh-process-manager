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
using Energinet.DataHub.Core.FunctionApp.TestCommon.FunctionAppHost;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ElectricityMarket.Integration.Options;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Options;
using WireMock.Server;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

/// <summary>
/// Support testing Example Orchestrations app and specifying configuration using inheritance.
/// This allows us to use multiple fixtures and coordinate their configuration.
/// </summary>
public class ExampleOrchestrationsAppManager : IAsyncDisposable
{
    /// <summary>
    /// Durable Functions Task Hub Name
    /// See naming constraints: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-task-hubs?tabs=csharp#task-hub-names
    /// </summary>
    private readonly string _taskHubName;

    private readonly int _appPort;
    private readonly bool _manageDatabase;
    private readonly bool _manageAzurite;

    public ExampleOrchestrationsAppManager()
        : this(
            new ProcessManagerDatabaseManager("ExampleOrchestrationsTest"),
            new IntegrationTestConfiguration(),
            new AzuriteManager(useOAuth: true),
            taskHubName: "ExampleOrchestrationsTest01",
            appPort: 8003,
            manageDatabase: true,
            manageAzurite: true)
    {
    }

    public ExampleOrchestrationsAppManager(
        ProcessManagerDatabaseManager databaseManager,
        IntegrationTestConfiguration configuration,
        AzuriteManager azuriteManager,
        string taskHubName,
        int appPort,
        bool manageDatabase,
        bool manageAzurite)
    {
        _taskHubName = string.IsNullOrWhiteSpace(taskHubName)
            ? throw new ArgumentException("Cannot be null or whitespace.", nameof(taskHubName))
            : taskHubName;
        _appPort = appPort;
        _manageDatabase = manageDatabase;
        _manageAzurite = manageAzurite;

        MockServer = WireMockServer.Start(port: 8013);
        DatabaseManager = databaseManager;
        TestLogger = new TestDiagnosticsLogger();

        IntegrationTestConfiguration = configuration;
        AzuriteManager = azuriteManager;
        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public WireMockServer MockServer { get; set; }

    public ProcessManagerDatabaseManager DatabaseManager { get; }

    public ITestDiagnosticsLogger TestLogger { get; }

    [NotNull]
    public FunctionAppHostManager? AppHostManager { get; private set; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private AzuriteManager AzuriteManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    /// <summary>
    /// Start the example orchestration app.
    /// </summary>
    /// <param name="processManagerTopicResources">Process Manager topic resources used by the app. Will be created if not provided.</param>
    /// <param name="ediTopicResources">EDI topic resources used by the app. Will be created if not provided.</param>
    public async Task StartAsync(
        ProcessManagerTopicResources? processManagerTopicResources,
        EdiTopicResources? ediTopicResources)
    {
        if (_manageAzurite)
        {
            AzuriteManager.CleanupAzuriteStorage();
            AzuriteManager.StartAzurite();
        }

        if (_manageDatabase)
            await DatabaseManager.CreateDatabaseAsync();

        processManagerTopicResources ??= await ProcessManagerTopicResources.CreateNew(ServiceBusResourceProvider);
        ediTopicResources ??= await EdiTopicResources.CreateNew(ServiceBusResourceProvider);

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings(
            "ProcessManager.Example.Orchestrations",
            processManagerTopicResources,
            ediTopicResources);

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
        EdiTopicResources ediTopicResources)
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

        // => Process Manager topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ServiceBusNamespaceOptions.SectionName}__{nameof(ServiceBusNamespaceOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerTopicOptions.SectionName}__{nameof(ProcessManagerTopicOptions.TopicName)}",
            processManagerTopicResources.ProcessManagerTopic.Name);

        // => Process Manager topic subscriptions
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerTopicOptions.SectionName}__{nameof(ProcessManagerTopicOptions.BrsX02SubscriptionName)}",
            processManagerTopicResources.BrsX02Subscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerTopicOptions.SectionName}__{nameof(ProcessManagerTopicOptions.BrsX03SubscriptionName)}",
            processManagerTopicResources.BrsX03Subscription.SubscriptionName);

        // => Edi topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{EdiTopicOptions.SectionName}__{nameof(EdiTopicOptions.Name)}",
            ediTopicResources.EdiTopic.Name);

        // => BRS-X01
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_X01_InputExample_V1.SectionName}__{nameof(OrchestrationOptions_Brs_X01_InputExample_V1.OptionValue)}",
            "options-example");

        // => BRS-X02
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_X02_NotifyOrchestrationInstanceExample_V1.SectionName}__{nameof(OrchestrationOptions_Brs_X02_NotifyOrchestrationInstanceExample_V1.WaitForExampleNotifyEventTimeout)}",
            TimeSpan.FromMinutes(10).ToString());

        // Electric Market client
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(ElectricityMarketClientOptions)}__{nameof(ElectricityMarketClientOptions.BaseUrl)}",
            MockServer.Url!);

        return appHostSettings;
    }

    /// <summary>
    /// Process Manager topic and subscription resources used by the Example Orchestrations app.
    /// </summary>
    public record ProcessManagerTopicResources(
        TopicResource ProcessManagerTopic,
        SubscriptionProperties BrsX02Subscription,
        SubscriptionProperties BrsX03Subscription)
    {
        private const string BrsX02SubscriptionName = "brs-x02-subscription";
        private const string BrsX03SubscriptionName = "brs-x03-subscription";

        public static async Task<ProcessManagerTopicResources> CreateNew(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var processManagerTopicBuilder = serviceBusResourceProvider.BuildTopic("pm-topic");
            AddSubscriptionsToTopicBuilder(processManagerTopicBuilder);

            var processManagerTopic = await processManagerTopicBuilder.CreateAsync();

            return CreateFromTopic(processManagerTopic);
        }

        /// <summary>
        /// Add the subscriptions used by the Example Orchestrations app to the topic builder.
        /// </summary>
        public static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(BrsX02SubscriptionName)
                    .AddSubjectFilter(Brs_X02_NotifyOrchestrationInstanceExample.Name);

            builder
                .AddSubscription(BrsX03SubscriptionName)
                    .AddSubjectFilter(Brs_X03.Name);

            return builder;
        }

        /// <summary>
        /// Get the <see cref="ExampleOrchestrationsAppManager.ProcessManagerTopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// This requires the Example Orchestrations app subscriptions to be created on the topic, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        public static ProcessManagerTopicResources CreateFromTopic(TopicResource topic)
        {
            var brsX02Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(BrsX02SubscriptionName));

            var brsX03Subscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(BrsX03SubscriptionName));

            return new ProcessManagerTopicResources(
                ProcessManagerTopic: topic,
                BrsX02Subscription: brsX02Subscription,
                BrsX03Subscription: brsX03Subscription);
        }
    }

    /// <summary>
    /// EDI topic and subscription resources used by the Example Orchestrations app.
    /// </summary>
    public record EdiTopicResources(
        TopicResource EdiTopic)
    {
        public static async Task<EdiTopicResources> CreateNew(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var ediTopicBuilder = serviceBusResourceProvider.BuildTopic("edi-topic");

            var ediTopic = await ediTopicBuilder.CreateAsync();

            return CreateFromTopic(ediTopic);
        }

        /// <summary>
        /// Get the <see cref="ExampleOrchestrationsAppManager.EdiTopicResources"/> used by the Orchestrations app.
        /// </summary>
        public static EdiTopicResources CreateFromTopic(TopicResource topic)
        {
            return new EdiTopicResources(
                EdiTopic: topic);
        }
    }
}
