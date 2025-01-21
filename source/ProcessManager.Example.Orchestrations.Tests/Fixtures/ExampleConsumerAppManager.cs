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
using Azure.ResourceManager.Resources;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.FunctionAppHost;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Consumer.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;

/// <summary>
/// Support running Example Consumer app and specifying configuration using inheritance.
/// This allows us to use multiple fixtures and coordinate their configuration.
/// </summary>
public class ExampleConsumerAppManager : IAsyncDisposable
{
    private readonly int _appPort;

    public ExampleConsumerAppManager()
        : this(
            new IntegrationTestConfiguration(),
            appPort: 8003)
    {
    }

    public ExampleConsumerAppManager(
        IntegrationTestConfiguration configuration,
        int appPort)
    {
        _appPort = appPort;
        TestLogger = new TestDiagnosticsLogger();

        IntegrationTestConfiguration = configuration;
        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public ITestDiagnosticsLogger TestLogger { get; }

    [NotNull]
    public FunctionAppHostManager? AppHostManager { get; private set; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    /// <summary>
    /// Start the example orchestration app.
    /// </summary>
    /// <param name="processManagerTopicResources">Process Manager topic resources used by the app. Will be created if not provided.</param>
    /// <param name="ediTopicResources">EDI topic resources used by the app. Will be created if not provided.</param>
    /// <param name="processManagerApiUrl">Base URL of the Process Manager general API.</param>
    /// <param name="orchestrationsApiUrl">Base URL of the Orchestrations API.</param>
    public async Task StartAsync(
        ProcessManagerTopicResources? processManagerTopicResources,
        EdiTopicResources? ediTopicResources,
        string processManagerApiUrl,
        string orchestrationsApiUrl)
    {
        processManagerTopicResources ??= await ProcessManagerTopicResources.CreateNew(ServiceBusResourceProvider);
        ediTopicResources ??= await EdiTopicResources.CreateNew(ServiceBusResourceProvider);

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings(
            "ProcessManager.Example.Consumer",
            processManagerTopicResources,
            ediTopicResources,
            processManagerApiUrl,
            orchestrationsApiUrl);

        // Create and start host
        AppHostManager = new FunctionAppHostManager(appHostSettings, TestLogger);
        StartHost(AppHostManager);
    }

    public async ValueTask DisposeAsync()
    {
        AppHostManager.Dispose();
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
        EdiTopicResources ediTopicResources,
        string processManagerGeneralApiBaseUrl,
        string orchestrationApiBaseUrl)
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
            "AZURE_LOG_LEVEL",
            "warning");
        appHostSettings.ProcessEnvironmentVariables.Add(
            "APPLICATIONINSIGHTS_CONNECTION_STRING",
            IntegrationTestConfiguration.ApplicationInsightsConnectionString);

        // ProcessManager
        // => Process Manager HTTP client
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerHttpClientsOptions.SectionName}__{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}",
            processManagerGeneralApiBaseUrl);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerHttpClientsOptions.SectionName}__{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}",
            orchestrationApiBaseUrl);

        // => Process Manager topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ServiceBusNamespaceOptions.SectionName}__{nameof(ServiceBusNamespaceOptions.FullyQualifiedNamespace)}",
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace);
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerServiceBusClientOptions.SectionName}__{nameof(ProcessManagerServiceBusClientOptions.TopicName)}",
            processManagerTopicResources.ProcessManagerTopic.Name);

        // => Edi topic
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{EdiTopicOptions.SectionName}__{nameof(EdiTopicOptions.Name)}",
            ediTopicResources.EdiTopic.Name);

        // => Enqueue BRS-X03
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{EdiTopicOptions.SectionName}__{nameof(EdiTopicOptions.EnqueueBrsX03SubscriptionName)}",
            ediTopicResources.EnqueueBrsX03Subscription.SubscriptionName);

        return appHostSettings;
    }

    /// <summary>
    /// Process Manager topic and subscription resources used by the Example Orchestrations app.
    /// </summary>
    public record ProcessManagerTopicResources(
        TopicResource ProcessManagerTopic)
    {
        public static async Task<ProcessManagerTopicResources> CreateNew(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var processManagerTopicBuilder = serviceBusResourceProvider.BuildTopic("pm-topic");

            var processManagerTopic = await processManagerTopicBuilder.CreateAsync();

            return CreateFromTopic(processManagerTopic);
        }

        /// <summary>
        /// Get the <see cref="ExampleConsumerAppManager.ProcessManagerTopicResources"/> used by the Orchestrations app.
        /// </summary>
        public static ProcessManagerTopicResources CreateFromTopic(TopicResource topic)
        {
            return new ProcessManagerTopicResources(
                ProcessManagerTopic: topic);
        }
    }

    /// <summary>
    /// EDI topic and subscription resources used by the Example Orchestrations app.
    /// </summary>
    public record EdiTopicResources(
        TopicResource EdiTopic,
        SubscriptionProperties EnqueueBrsX03Subscription)
    {
        private const string EnqueueBrsX03SubscriptionName = $"enqueue-brs-x03-subscription";

        public static async Task<EdiTopicResources> CreateNew(ServiceBusResourceProvider serviceBusResourceProvider)
        {
            var ediTopicBuilder = serviceBusResourceProvider.BuildTopic("edi-topic");
            AddSubscriptionsToTopicBuilder(ediTopicBuilder);
            var ediTopic = await ediTopicBuilder.CreateAsync();

            return CreateFromTopic(ediTopic);
        }

        /// <summary>
        /// Add the subscriptions used by the Example Consumer app to the topic builder.
        /// </summary>
        public static TopicResourceBuilder AddSubscriptionsToTopicBuilder(TopicResourceBuilder builder)
        {
            builder
                .AddSubscription(EnqueueBrsX03SubscriptionName)
                    .AddSubjectFilter($"Enqueue_{Brs_X03.Name.ToLower()}");

            return builder;
        }

        /// <summary>
        /// Get the <see cref="ExampleConsumerAppManager.EdiTopicResources"/> used by the Orchestrations app.
        /// <remarks>
        /// The required subscriptions must be added to the topic by calling <see cref="AddSubscriptionsToTopicBuilder"/>.
        /// </remarks>
        /// </summary>
        public static EdiTopicResources CreateFromTopic(TopicResource topic)
        {
            var enqueueBrsX03Subscription = topic.Subscriptions
                .Single(s => s.SubscriptionName == EnqueueBrsX03SubscriptionName);

            return new EdiTopicResources(
                EdiTopic: topic,
                EnqueueBrsX03Subscription: enqueueBrsX03Subscription);
        }
    }
}
