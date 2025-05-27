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

        DatabaseManager = databaseManager;
        TestLogger = new TestDiagnosticsLogger();

        IntegrationTestConfiguration = configuration;
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

    [NotNull]
    public TopicResource? ProcessManagerNotifyTopic { get; private set; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private AzuriteManager AzuriteManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    /// <summary>
    /// Start the Process Manager core app.
    /// </summary>
    public async Task StartAsync()
    {
        if (_manageAzurite)
        {
            AzuriteManager.CleanupAzuriteStorage();
            AzuriteManager.StartAzurite();
        }

        if (_manageDatabase)
            await DatabaseManager.CreateDatabaseAsync();

        // Creates Process Manager default Notify topic and subscription
        var notifyTopicResources = await ProcessManagerNotifyTopicResources.CreateNewAsync(ServiceBusResourceProvider);
        ProcessManagerNotifyTopic = notifyTopicResources.NotifyTopic;

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings("ProcessManager", notifyTopicResources);

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

        await ServiceBusResourceProvider.DisposeAsync();
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

    private FunctionAppHostSettings CreateAppHostSettings(
        string csprojName,
        ProcessManagerNotifyTopicResources notifyTopicResources)
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

        // => Process Manager Notify topic and subscription
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerNotifyTopicOptions.SectionName}__{nameof(ProcessManagerNotifyTopicOptions.TopicName)}",
            notifyTopicResources.NotifyTopic.Name);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerNotifyTopicOptions.SectionName}__{nameof(ProcessManagerNotifyTopicOptions.SubscriptionName)}",
            notifyTopicResources.NotifySubscription.SubscriptionName);

        // Disable timer triggers (should be manually triggered in tests)
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"AzureWebJobs.StartScheduledOrchestrationInstances.Disabled",
            "true");
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"AzureWebJobs.PerformRecurringPlanning.Disabled",
            "true");

        return appHostSettings;
    }

    /// <summary>
    /// Process Manager default notify topic and subscription resources used by the Process Manager core app.
    /// </summary>
    private record ProcessManagerNotifyTopicResources(
        TopicResource NotifyTopic,
        SubscriptionProperties NotifySubscription)
    {
        private const string NotifySubscriptionName = "pm-notify";

        internal static async Task<ProcessManagerNotifyTopicResources> CreateNewAsync(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var topic = await serviceBusResourceProvider
                .BuildTopic("pm-notify-topic")
                .AddSubscription(NotifySubscriptionName)
                    .AddSubjectFilter("NotifyOrchestration")
                .CreateAsync();

            return new ProcessManagerNotifyTopicResources(
                NotifyTopic: topic,
                NotifySubscription: topic.Subscriptions.Single());
        }
    }
}
