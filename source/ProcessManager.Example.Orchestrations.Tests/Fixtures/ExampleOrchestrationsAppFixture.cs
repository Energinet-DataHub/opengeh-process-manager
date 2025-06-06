﻿// Copyright 2020 Energinet DataHub A/S
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
using AutoFixture;
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;

/// <summary>
/// Support testing the interactions with ProcessManager.Example.Orchestrations and
/// Process Manager Api, by coordinating the startup of the dependent applications
/// ProcessManager.Example.Orchestrations and ProcessManager (Api).
/// </summary>
public class ExampleOrchestrationsAppFixture : IAsyncLifetime
{
    private const string TaskHubName = "ExampleOrchestrationsAppTest01";

    public ExampleOrchestrationsAppFixture()
    {
        DatabaseManager = new ProcessManagerDatabaseManager("ExampleOrchestrationsAppTests");
        AzuriteManager = new AzuriteManager(useOAuth: true);

        IntegrationTestConfiguration = new IntegrationTestConfiguration();

        ExampleOrchestrationsAppManager = new ExampleOrchestrationsAppManager(
            DatabaseManager,
            IntegrationTestConfiguration,
            AzuriteManager,
            taskHubName: TaskHubName,
            appPort: 8301,
            manageDatabase: false,
            manageAzurite: false);

        ProcessManagerAppManager = new ProcessManagerAppManager(
            DatabaseManager,
            IntegrationTestConfiguration,
            AzuriteManager,
            taskHubName: TaskHubName,
            appPort: 8302,
            manageDatabase: false,
            manageAzurite: false);

        ExampleConsumerAppManager = new ExampleConsumerAppManager(
            IntegrationTestConfiguration,
            appPort: 8303);

        DurableTaskManager = new DurableTaskManager(
            "AzuriteConnectionString",
            AzuriteManager.FullConnectionString);

        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            ExampleOrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);

        EnqueueBrs101ServiceBusListener = new ServiceBusListenerMock(
            ExampleOrchestrationsAppManager.TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    public ExampleOrchestrationsAppManager ExampleOrchestrationsAppManager { get; }

    public ProcessManagerAppManager ProcessManagerAppManager { get; }

    public ExampleConsumerAppManager ExampleConsumerAppManager { get; }

    [NotNull]
    public IDurableClient? DurableClient { get; private set; }

    public ServiceBusListenerMock EnqueueBrs101ServiceBusListener { get; }

    public ActorIdentityDto DefaultActorIdentity => new ActorIdentityDto(
        ActorNumber.Create("1234567890123"),
        ActorRole.EnergySupplier);

    public UserIdentityDto DefaultUserIdentity => new UserIdentityDto(
        Guid.NewGuid(),
        DefaultActorIdentity.ActorNumber,
        DefaultActorIdentity.ActorRole);

    /// <summary>
    /// Process Manager http client.
    /// </summary>
    [NotNull]
    public IProcessManagerClient? ProcessManagerClient { get; private set; }

    /// <summary>
    /// Process Manager messages client.
    /// </summary>
    [NotNull]
    public IProcessManagerMessageClient? ProcessManagerMessageClient { get; private set; }

    private ProcessManagerDatabaseManager DatabaseManager { get; }

    private AzuriteManager AzuriteManager { get; }

    private DurableTaskManager DurableTaskManager { get; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    [NotNull]
    private ServiceProvider? ServiceProvider { get; set; }

    public async Task InitializeAsync()
    {
        AzuriteManager.CleanupAzuriteStorage();
        AzuriteManager.StartAzurite();

        await DatabaseManager.CreateDatabaseAsync();

        // Start Process Manager app
        // => Creates Process Manager default Notify topic and subscription
        await ProcessManagerAppManager.StartAsync();

        // Creates EDI enqueue actor messages topic and subscriptions
        var ediTopicBuilder = ServiceBusResourceProvider.BuildTopic("edi-topic");
        ExampleOrchestrationsAppManager.EdiEnqueueTopicResources.AddSubscriptionsToTopicBuilder(ediTopicBuilder);
        ExampleConsumerAppManager.EdiEnqueueTopicResources.AddSubscriptionsToTopicBuilder(ediTopicBuilder);
        var ediTopicResource = await ediTopicBuilder.CreateAsync();
        var ediEnqueueTopicResources = ExampleOrchestrationsAppManager.EdiEnqueueTopicResources.CreateFromTopic(ediTopicResource);
        // => Create listeners for enqueue messages
        await EnqueueBrs101ServiceBusListener.AddTopicSubscriptionListenerAsync(
            ediEnqueueTopicResources.Brs101UpdateMeteringPointConnectionStateSubscription.TopicName,
            ediEnqueueTopicResources.Brs101UpdateMeteringPointConnectionStateSubscription.SubscriptionName);

        // Start Example Orchestrations app
        // => Creates Process Manager default Start topics and subscriptions
        // => Creates BRS-021 Forward Metered Data Start/Notify topics and subscriptions
        await ExampleOrchestrationsAppManager.StartAsync(ediEnqueueTopicResources);

        // Start Example Consumer app
        await ExampleConsumerAppManager.StartAsync(
            ExampleOrchestrationsAppManager.ProcessManagerStartTopic,
            ProcessManagerAppManager.ProcessManagerNotifyTopic,
            ExampleOrchestrationsAppManager.Brs021ForwardMeteredDataStartTopic,
            ExampleOrchestrationsAppManager.Brs021ForwardMeteredDataNotifyTopic,
            ExampleConsumerAppManager.EdiEnqueueTopicResources.CreateFromTopic(ediTopicResource),
            processManagerApiUrl: ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.AbsoluteUri,
            orchestrationsApiUrl: ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.AbsoluteUri);

        // Create durable client when TaskHub has been created
        DurableClient = DurableTaskManager.CreateClient(taskHubName: TaskHubName);

        // Prepare clients
        ServiceProvider = ConfigureProcessManagerClients();
        ProcessManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
        ProcessManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
    }

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        await ExampleConsumerAppManager.DisposeAsync();
        await ExampleOrchestrationsAppManager.DisposeAsync();
        await ProcessManagerAppManager.DisposeAsync();
        await DurableTaskManager.DisposeAsync();
        await DatabaseManager.DeleteDatabaseAsync();
        await EnqueueBrs101ServiceBusListener.DisposeAsync();
        await ServiceBusResourceProvider.DisposeAsync();

        AzuriteManager.Dispose();
    }

    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        ExampleOrchestrationsAppManager.SetTestOutputHelper(testOutputHelper);
        ProcessManagerAppManager.SetTestOutputHelper(testOutputHelper);
        ExampleConsumerAppManager.SetTestOutputHelper(testOutputHelper);
    }

    /// <summary>
    /// Register and configure services for Process Manager http and messages clients.
    /// </summary>
    private ServiceProvider ConfigureProcessManagerClients()
    {
        var services = new ServiceCollection();
        services
            .AddTokenCredentialProvider()
            .AddInMemoryConfiguration(new Dictionary<string, string?>
            {
                // Process Manager HTTP client
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                    = SubsystemAuthenticationOptionsForTests.ApplicationIdUri,
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                    = ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                    = ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),

                // Process Manager message client
                [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.StartTopicName)}"]
                    = ExampleOrchestrationsAppManager.ProcessManagerStartTopic.Name,
                [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.NotifyTopicName)}"]
                    = ProcessManagerAppManager.ProcessManagerNotifyTopic.Name,
                [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataStartTopicName)}"]
                    = ExampleOrchestrationsAppManager.Brs021ForwardMeteredDataStartTopic.Name,
                [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataNotifyTopicName)}"]
                    = ExampleOrchestrationsAppManager.Brs021ForwardMeteredDataNotifyTopic.Name,
            });

        // Process Manager HTTP client
        services.AddProcessManagerHttpClients();

        // Process Manager message client
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerMessageClient();

        return services.BuildServiceProvider();
    }
}
