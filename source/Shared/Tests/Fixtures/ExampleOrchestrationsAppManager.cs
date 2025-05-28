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
using Energinet.DataHub.Core.App.Common.Extensions.Options;
using Energinet.DataHub.Core.FunctionApp.TestCommon.AppConfiguration;
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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Options;
using WireMock.Server;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

/// <summary>
/// Support testing Example Orchestrations app and specifying configuration.
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
    /// Start the Example Orchestrations app.
    /// </summary>
    /// <param name="ediEnqueueTopicResources">EDI enqueue actor messages topic resources used by the app.
    /// Will be created if not provided.</param>
    public async Task StartAsync(
        EdiEnqueueTopicResources? ediEnqueueTopicResources)
    {
        if (_manageAzurite)
        {
            AzuriteManager.CleanupAzuriteStorage();
            AzuriteManager.StartAzurite();
        }

        if (_manageDatabase)
            await DatabaseManager.CreateDatabaseAsync();

        // Creates Process Manager default Start topics and subscriptions
        var startTopicResources = await ProcessManagerStartTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        ProcessManagerStartTopic = startTopicResources.StartTopic;

        // Creates BRS-021 Forward Metered Data Start/Notify topics and subscriptions
        var brs021fmdTopicResource = await Brs021ForwardMeteredDataTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        Brs021ForwardMeteredDataStartTopic = brs021fmdTopicResource.StartTopic;
        Brs021ForwardMeteredDataNotifyTopic = brs021fmdTopicResource.NotifyTopic;

        // Creates EDI enqueue actor messages topic and subscriptions
        ediEnqueueTopicResources ??= await EdiEnqueueTopicResources.CreateNewAsync(ServiceBusResourceProvider);

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings(
            "ProcessManager.Example.Orchestrations",
            startTopicResources,
            brs021fmdTopicResource,
            ediEnqueueTopicResources);

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
        Brs021ForwardMeteredDataTopicResources brs021fmdTopicResources,
        EdiEnqueueTopicResources ediEnqueueTopicResources)
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

        // Feature Management => Azure App Configuration settings
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{AzureAppConfigurationOptions.SectionName}:{nameof(AzureAppConfigurationOptions.Endpoint)}",
            IntegrationTestConfiguration.AppConfigurationEndpoint);
        appHostSettings.ProcessEnvironmentVariables.Add(
            AppConfigurationManager.DisableProviderSettingName,
            "true");

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
        // => File Storage
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerFileStorageOptions.SectionName}__{nameof(ProcessManagerFileStorageOptions.ServiceUri)}",
            AzuriteManager.BlobStorageServiceUri.AbsoluteUri);
        // => Authentication
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{SubsystemAuthenticationOptions.SectionName}__{nameof(SubsystemAuthenticationOptions.ApplicationIdUri)}",
            SubsystemAuthenticationOptionsForTests.ApplicationIdUri);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{SubsystemAuthenticationOptions.SectionName}__{nameof(SubsystemAuthenticationOptions.Issuer)}",
            SubsystemAuthenticationOptionsForTests.Issuer);

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
            startTopicResources.Brs101UpdateMeteringPointConnectionStateSubscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.BrsX02NotifyOrchestrationInstanceExampleSubscriptionName)}",
            startTopicResources.BrsX02NotifyOrchestrationInstanceExampleSubscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerStartTopicOptions.SectionName}__{nameof(ProcessManagerStartTopicOptions.BrsX02ActorRequestProcessExampleSubscriptionName)}",
            startTopicResources.BrsX02ActorRequestProcessExampleSubscription.SubscriptionName);

        // => BRS-021 Forward Metered Data topics and subscriptions
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.StartTopicName)}",
            brs021fmdTopicResources.StartTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.NotifyTopicName)}",
            brs021fmdTopicResources.NotifyTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.StartSubscriptionName)}",
            brs021fmdTopicResources.StartSubscription.SubscriptionName);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{Brs021ForwardMeteredDataTopicOptions.SectionName}__{nameof(Brs021ForwardMeteredDataTopicOptions.NotifySubscriptionName)}",
            brs021fmdTopicResources.NotifySubscription.SubscriptionName);

        // => Edi enqueue topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{EdiTopicOptions.SectionName}__{nameof(EdiTopicOptions.Name)}",
            ediEnqueueTopicResources.EnqueueTopic.Name);

        // => BRS-101 Update MeteringPoint Connection State options
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_101_UpdateMeteringPointConnectionState_V1.SectionName}__{nameof(OrchestrationOptions_Brs_101_UpdateMeteringPointConnectionState_V1.EnqueueActorMessagesTimeout)}",
            TimeSpan.FromMinutes(10).ToString());

        // => BRS-X01 options
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_X01_InputExample_V1.SectionName}__{nameof(OrchestrationOptions_Brs_X01_InputExample_V1.OptionValue)}",
            "options-example");

        // => BRS-X02 options
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{OrchestrationOptions_Brs_X02_NotifyOrchestrationInstanceExample_V1.SectionName}__{nameof(OrchestrationOptions_Brs_X02_NotifyOrchestrationInstanceExample_V1.WaitForExampleNotifyEventTimeout)}",
            TimeSpan.FromMinutes(10).ToString());

        // Electric Market client
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(ElectricityMarketClientOptions)}__{nameof(ElectricityMarketClientOptions.BaseUrl)}",
            MockServer.Url!);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{nameof(ElectricityMarketClientOptions)}__{nameof(ElectricityMarketClientOptions.ApplicationIdUri)}",
            SubsystemAuthenticationOptionsForTests.ApplicationIdUri);

        return appHostSettings;
    }

    /// <summary>
    /// EDI enqueue actor messages topic and subscription resources used by the Example Orchestrations app.
    /// </summary>
    public record EdiEnqueueTopicResources(
        TopicResource EnqueueTopic,
        SubscriptionProperties Brs101UpdateMeteringPointConnectionStateSubscription)
    {
        private const string Brs101UpdateMeteringPointConnectionStateSubscriptionName = "brs-101-updatemeteringpointconnectionstate";

        public static async Task<EdiEnqueueTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var topicBuilder = serviceBusResourceProvider.BuildTopic("edi-topic");
            AddSubscriptionsToTopicBuilder(topicBuilder);

            var topic = await topicBuilder.CreateAsync();
            return CreateFromTopic(topic);
        }

        /// <summary>
        /// Add the subscriptions used by the Example Orchestrations app to the topic builder.
        /// </summary>
        public static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(Brs101UpdateMeteringPointConnectionStateSubscriptionName)
                    .AddSubjectFilter(EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_101_UpdateMeteringPointConnectionState.V1));

            return builder;
        }

        /// <summary>
        /// Get the <see cref="EdiEnqueueTopicResources"/> used by the Example Orchestrations app.
        /// <remarks>
        /// Subscriptions must be created on the topic beforehand, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        public static EdiEnqueueTopicResources CreateFromTopic(TopicResource topic)
        {
            var brs101UpdateMeteringPointConnectionStateSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(Brs101UpdateMeteringPointConnectionStateSubscriptionName));

            return new EdiEnqueueTopicResources(
                EnqueueTopic: topic,
                Brs101UpdateMeteringPointConnectionStateSubscription: brs101UpdateMeteringPointConnectionStateSubscription);
        }
    }

    /// <summary>
    /// Process Manager default start topic and subscription resources used by the Example Orchestrations app.
    /// </summary>
    private record ProcessManagerStartTopicResources(
        TopicResource StartTopic,
        SubscriptionProperties Brs101UpdateMeteringPointConnectionStateSubscription,
        SubscriptionProperties BrsX02NotifyOrchestrationInstanceExampleSubscription,
        SubscriptionProperties BrsX02ActorRequestProcessExampleSubscription)
    {
        private const string Brs101UpdateMeteringPointConnectionStateSubscriptionName = "brs-101-updatemeteringpointconnectionstate";
        private const string BrsX02NotifyOrchestrationInstanceExampleSubscriptionName = "brs-x02-notifyorchestrationinstanceexample";
        private const string BrsX02ActorRequestProcessExampleSubscriptionName = "brs-x02-actorrequestprocessexample";

        internal static async Task<ProcessManagerStartTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var topicBuilder = serviceBusResourceProvider.BuildTopic("pm-start-topic");
            AddSubscriptionsToTopicBuilder(topicBuilder);

            var topic = await topicBuilder.CreateAsync();
            return CreateFromTopic(topic);
        }

        /// <summary>
        /// Add the subscriptions used by the Example Orchestrations app to the topic builder.
        /// </summary>
        private static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(Brs101UpdateMeteringPointConnectionStateSubscriptionName)
                    .AddSubjectFilter(Brs_101_UpdateMeteringPointConnectionState.Name)
                .AddSubscription(BrsX02NotifyOrchestrationInstanceExampleSubscriptionName)
                    .AddSubjectFilter(Brs_X02_NotifyOrchestrationInstanceExample.Name)
                .AddSubscription(BrsX02ActorRequestProcessExampleSubscriptionName)
                    .AddSubjectFilter(Brs_X02_ActorRequestProcessExample.Name);

            return builder;
        }

        /// <summary>
        /// Get the <see cref="ProcessManagerStartTopicResources"/> used by the Example Orchestrations app.
        /// <remarks>
        /// Subscriptions must be created on the topic beforehand, using <see cref="AddSubscriptionsToTopicBuilder"/>.
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
                Brs101UpdateMeteringPointConnectionStateSubscription: brs101UpdateMeteringPointConnectionStateSubscription,
                BrsX02NotifyOrchestrationInstanceExampleSubscription: brsX02NotifyOrchestrationInstanceExampleSubscription,
                BrsX02ActorRequestProcessExampleSubscription: brsX02ActorRequestProcessExampleSubscription);
        }
    }

    /// <summary>
    /// BRS-021 Forward Metered Data start + notify topic and subscription resources used by the Example Orchestrations app.
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
