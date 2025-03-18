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
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_101.UpdateMeteringPointConnectionState;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.ActorRequestProcessExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample;
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

    [NotNull]
    public TopicResource? ProcessManagerStartTopic { get; private set; }

    [NotNull]
    public TopicResource? Brs021ForwardMeteredDataStartTopic { get; private set; }

    [NotNull]
    public TopicResource? Brs021ForwardMeteredDataNotifyTopic { get; private set; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private AzuriteManager AzuriteManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    /// <summary>
    /// Start the example orchestration app.
    /// </summary>
    /// <param name="ediTopicResources">EDI topic resources used by the app. Will be created if not provided.</param>
    public async Task StartAsync(
        EdiTopicResources? ediTopicResources)
    {
        if (_manageAzurite)
        {
            AzuriteManager.CleanupAzuriteStorage();
            AzuriteManager.StartAzurite();
        }

        if (_manageDatabase)
            await DatabaseManager.CreateDatabaseAsync();

        // Orchestrations service bus topics
        var startTopicResources = await ProcessManagerStartTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        ProcessManagerStartTopic = startTopicResources.StartTopic;
        var brs21TopicResource = await Brs21TopicResources.CreateNewAsync(ServiceBusResourceProvider);
        Brs021ForwardMeteredDataStartTopic = brs21TopicResource.Brs21StartTopic;
        Brs021ForwardMeteredDataNotifyTopic = brs21TopicResource.Brs21NotifyTopic;

        // EDI topic
        ediTopicResources ??= await EdiTopicResources.CreateNewAsync(ServiceBusResourceProvider);

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings(
            "ProcessManager.Example.Orchestrations",
            startTopicResources,
            ediTopicResources,
            brs21TopicResource);

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
        ProcessManagerStartTopicResources startTopicResources,
        EdiTopicResources ediTopicResources,
        Brs21TopicResources brs21TopicResources)
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
            startTopicResources.StartTopic.Name);
        // => Process Manager Start topic -> subscriptions
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.Brs101UpdateMeteringPointConnectionStateSubscriptionName)}",
            startTopicResources.StartBrs101UpdateMeteringPointConnectionStateSubscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.BrsX02NotifyOrchestrationInstanceExampleSubscriptionName)}",
            startTopicResources.StartBrsX02NotifyOrchestrationInstanceExampleSubscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.BrsX02ActorRequestProcessExampleSubscriptionName)}",
            startTopicResources.StartBrsX02ActorRequestProcessExampleSubscription.SubscriptionName);

        // => BRS-021 Forward Metered Data topics
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
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(ElectricityMarketClientOptions)}__{nameof(ElectricityMarketClientOptions.ApplicationIdUri)}",
            AuthenticationOptionsForTests.ApplicationIdUri);

        return appHostSettings;
    }

    /// <summary>
    /// Process Manager default start topic and subscription resources used by the Example Orchestrations app.
    /// </summary>
    public record ProcessManagerStartTopicResources(
        TopicResource StartTopic,
        SubscriptionProperties StartBrs101UpdateMeteringPointConnectionStateSubscription,
        SubscriptionProperties StartBrsX02NotifyOrchestrationInstanceExampleSubscription,
        SubscriptionProperties StartBrsX02ActorRequestProcessExampleSubscription)
    {
        private const string Brs101UpdateMeteringPointConnectionStateSubscriptionName = "start-brs-101-updatemeteringpointconnectionstate";
        private const string BrsX02NotifyOrchestrationInstanceExampleSubscriptionName = "start-brs-x02-notifyorchestrationinstanceexample";
        private const string BrsX02ActorRequestProcessExampleSubscriptionName = "start-brs-x02-actorrequestprocessexample";

        internal static async Task<ProcessManagerStartTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var processManagerTopicBuilder = serviceBusResourceProvider.BuildTopic("pm-start-topic");
            AddSubscriptionsToTopicBuilder(processManagerTopicBuilder);

            var processManagerTopic = await processManagerTopicBuilder.CreateAsync();

            return CreateFromTopic(processManagerTopic);
        }

        /// <summary>
        /// Add the subscriptions used by the Example Orchestrations app to the topic builder.
        /// </summary>
        private static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(Brs101UpdateMeteringPointConnectionStateSubscriptionName)
                    .AddSubjectFilter(Brs_101_UpdateMeteringPointConnectionState.Name);

            builder
                .AddSubscription(BrsX02NotifyOrchestrationInstanceExampleSubscriptionName)
                    .AddSubjectFilter(Brs_X02_NotifyOrchestrationInstanceExample.Name);

            builder
                .AddSubscription(BrsX02ActorRequestProcessExampleSubscriptionName)
                    .AddSubjectFilter(Brs_X02_ActorRequestProcessExample.Name);

            return builder;
        }

        /// <summary>
        /// Get the <see cref="ExampleOrchestrationsAppManager.ProcessManagerStartTopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// This requires the Example Orchestrations app subscriptions to be created on the topic, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        private static ProcessManagerStartTopicResources CreateFromTopic(TopicResource topic)
        {
            var brs101UpdateMeteringPointConnectionStateSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs101UpdateMeteringPointConnectionStateSubscriptionName));

            var brsX02NotifyOrchestrationInstanceExampleSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(BrsX02NotifyOrchestrationInstanceExampleSubscriptionName));

            var brsX02ActorRequestProcessExampleSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(BrsX02ActorRequestProcessExampleSubscriptionName));

            return new ProcessManagerStartTopicResources(
                StartTopic: topic,
                StartBrs101UpdateMeteringPointConnectionStateSubscription: brs101UpdateMeteringPointConnectionStateSubscription,
                StartBrsX02NotifyOrchestrationInstanceExampleSubscription: brsX02NotifyOrchestrationInstanceExampleSubscription,
                StartBrsX02ActorRequestProcessExampleSubscription: brsX02ActorRequestProcessExampleSubscription);
        }
    }

    /// <summary>
    /// EDI topic and subscription resources used by the Example Orchestrations app.
    /// </summary>
    public record EdiTopicResources(
        TopicResource EdiTopic,
        SubscriptionProperties EnqueueBrs101UpdateMeteringPointConnectionStateSubscription)
    {
        private const string EnqueueBrs101UpdateMeteringPointConnectionStateSubscriptionName = "enqueue-brs-101-updatemeteringpointconnectionstate";

        public static async Task<EdiTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
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
                .AddSubscription(EnqueueBrs101UpdateMeteringPointConnectionStateSubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_101_UpdateMeteringPointConnectionState.V1));

            return builder;
        }

        /// <summary>
        /// Get the <see cref="ExampleOrchestrationsAppManager.EdiTopicResources"/> used by the Example Orchestrations app.
        /// </summary>
        public static EdiTopicResources CreateFromTopic(TopicResource topic)
        {
            var brs101UpdateMeteringPointConnectionStateSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(EnqueueBrs101UpdateMeteringPointConnectionStateSubscriptionName));

            return new EdiTopicResources(
                EdiTopic: topic,
                EnqueueBrs101UpdateMeteringPointConnectionStateSubscription: brs101UpdateMeteringPointConnectionStateSubscription);
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

        internal static async Task<Brs21TopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
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
        internal static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder, string subscriptionName)
        {
            builder
                .AddSubscription(subscriptionName);

            return builder;
        }

        /// <summary>
        /// Get the <see cref="ExampleOrchestrationsAppManager.Brs21TopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// This requires the Orchestration subscriptions to be created on the topic, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        internal static SubscriptionProperties GetSubscription(TopicResource topic, string subscriptionName)
        {
            return topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(subscriptionName));
        }
    }
}
