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
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Extensions.Options;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

/// <summary>
/// Support testing Process Manager app and specifying configuration.
/// This allows us to use multiple apps and coordinate their configuration.
/// </summary>
public class ProcessManagerAppManager : IAsyncDisposable
{
    /// <summary>
    /// Durable Functions Task Hub Name
    /// See naming constraints: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-task-hubs?tabs=csharp#task-hub-names
    /// </summary>
    private readonly string _taskHubName;

    private readonly int _appPort;
    private readonly bool _manageDatabase;
    private readonly bool _manageAzurite;

    public ProcessManagerAppManager()
        : this(
            new ProcessManagerDatabaseManager("ProcessManagerTest"),
            new IntegrationTestConfiguration(),
            new AzuriteManager(useOAuth: true),
            taskHubName: "ProcessManagerTest01",
            appPort: 8001,
            manageDatabase: true,
            manageAzurite: true)
    {
    }

    public ProcessManagerAppManager(
        ProcessManagerDatabaseManager databaseManager,
        IntegrationTestConfiguration integrationTestConfiguration,
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

        DatabaseManager = databaseManager;
        TestLogger = new TestDiagnosticsLogger();

        IntegrationTestConfiguration = integrationTestConfiguration;
        AzuriteManager = azuriteManager;
        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public ProcessManagerDatabaseManager DatabaseManager { get; }

    public ITestDiagnosticsLogger TestLogger { get; }

    [NotNull]
    public FunctionAppHostManager? AppHostManager { get; private set; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private AzuriteManager AzuriteManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    /// <summary>
    /// Start the app.
    /// </summary>
    /// <param name="processManagerTopicResources">The required service bus resources. If not provided then the method will create them.</param>
    public async Task StartAsync(ProcessManagerTopicResources? processManagerTopicResources)
    {
        if (_manageAzurite)
        {
            AzuriteManager.CleanupAzuriteStorage();
            AzuriteManager.StartAzurite();
        }

        if (_manageDatabase)
            await DatabaseManager.CreateDatabaseAsync();

        processManagerTopicResources ??= await ProcessManagerTopicResources.CreateNew(ServiceBusResourceProvider);

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings("ProcessManager", processManagerTopicResources);

        // Create and start host
        AppHostManager = new FunctionAppHostManager(appHostSettings, TestLogger);
        StartHost(AppHostManager);
    }

    public async ValueTask DisposeAsync()
    {
        AppHostManager?.Dispose();

        if (_manageAzurite)
            AzuriteManager.Dispose();

        if (_manageDatabase)
            await DatabaseManager.DeleteDatabaseAsync();
    }

    /// <summary>
    /// Use this method to attach <paramref name="testOutputHelper"/> to the host logging pipeline.
    /// While attached, any entries written to host log pipeline will also be logged to xUnit test output.
    /// It is important that it is only attached while a test i active. Hence, it should be attached in
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

    private FunctionAppHostSettings CreateAppHostSettings(string csprojName, ProcessManagerTopicResources processManagerTopicResources)
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

        // Service Bus
        // => Service Bus Client
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ServiceBusNamespaceOptions.SectionName}__{nameof(ServiceBusNamespaceOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace);
        // => NotifyOrchestrationInstance topic/subscription
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{NotifyOrchestrationInstanceOptions.SectionName}__{nameof(NotifyOrchestrationInstanceOptions.TopicName)}",
            "Not used in tests anymore, but cannot be empty");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{NotifyOrchestrationInstanceOptions.SectionName}__{nameof(NotifyOrchestrationInstanceOptions.NotifyOrchestrationInstanceSubscriptionName)}",
            "Not used in tests anymore, but cannot be empty");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerNotifyTopicOptions.SectionName}__{nameof(ProcessManagerNotifyTopicOptions.TopicName)}",
            processManagerTopicResources.ProcessManagerTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerNotifyTopicOptions.SectionName}__{nameof(ProcessManagerNotifyTopicOptions.SubscriptionName)}",
            processManagerTopicResources.NotifyOrchestrationInstanceSubscription.SubscriptionName);

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

        // Disable timer triggers (should be manually triggered in tests)
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"AzureWebJobs.StartScheduledOrchestrationInstances.Disabled",
            "true");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"AzureWebJobs.PerformRecurringPlanning.Disabled",
            "true");

        return appHostSettings;
    }

    public record ProcessManagerTopicResources(
        TopicResource ProcessManagerTopic,
        SubscriptionProperties NotifyOrchestrationInstanceSubscription)
    {
        private const string NotifyOrchestrationInstanceSubscriptionName = "notify-orchestration-instance-subscription";

        public static async Task<ProcessManagerTopicResources> CreateNew(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            // Process Manager topic & subscriptions
            var processManagerTopicBuilder = serviceBusResourceProvider.BuildTopic("pm-topic");
            AddSubscriptionsToTopicBuilder(processManagerTopicBuilder);

            var processManagerTopic = await processManagerTopicBuilder.CreateAsync();

            return CreateFromTopic(processManagerTopic);
        }

        /// <summary>
        /// Add the subscriptions used by the Process Manager app to the topic builder.
        /// </summary>
        public static TopicSubscriptionBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            return builder
                .AddSubscription(NotifyOrchestrationInstanceSubscriptionName)
                    .AddSubjectFilter("NotifyOrchestration");
        }

        /// <summary>
        /// Get the <see cref="ProcessManagerAppManager.ProcessManagerTopicResources"/> used by the Process Manager app.
        /// <remarks>
        /// This requires the Process Manager subscriptions to be created on the topic, using <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        public static ProcessManagerTopicResources CreateFromTopic(TopicResource topic)
        {
            var notifyOrchestrationInstanceSubscription = topic.Subscriptions
                .Single(x => x.SubscriptionName.Equals(NotifyOrchestrationInstanceSubscriptionName));

            return new ProcessManagerTopicResources(
                topic,
                notifyOrchestrationInstanceSubscription);
        }
    }
}
