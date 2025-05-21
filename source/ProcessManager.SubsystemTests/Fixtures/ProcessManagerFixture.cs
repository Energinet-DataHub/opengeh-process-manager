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

using System.Diagnostics.CodeAnalysis;
using Azure.Identity;
using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;

public class ProcessManagerFixture<TConfiguration> : IAsyncLifetime
{
    private const string ProcessManagerEventHubProducerClientName = "ProcessManagerEventHubClient";

    private readonly Guid _subsystemTestUserId = Guid.Parse("00000000-0000-0000-0000-000000000999");

    private readonly ServiceProvider _services;

    public ProcessManagerFixture()
    {
        Logger = new TestDiagnosticsLogger();

        Configuration = new ProcessManagerSubsystemTestConfiguration();

        var serviceCollection = BuildServices();
        _services = serviceCollection.BuildServiceProvider();

        TestConfiguration = default;
    }

    public ProcessManagerSubsystemTestConfiguration Configuration { get; }

    public TestDiagnosticsLogger Logger { get; }

    [NotNull]
    public TConfiguration? TestConfiguration { get; set; }

    public IProcessManagerMessageClient ProcessManagerMessageClient => _services.GetRequiredService<IProcessManagerMessageClient>();

    public IProcessManagerClient ProcessManagerHttpClient => _services.GetRequiredService<IProcessManagerClient>();

    public EventHubProducerClient ProcessManagerEventHubProducerClient => _services
        .GetRequiredService<IAzureClientFactory<EventHubProducerClient>>()
        .CreateClient(ProcessManagerEventHubProducerClientName);

    [NotNull]
    public ActorIdentityDto? EnergySupplierActorIdentity { get; private set; }

    [NotNull]
    public UserIdentityDto? EnergySupplierUserIdentity { get; private set; }

    [NotNull]
    public ActorIdentityDto? GridAccessProviderActorIdentity { get; private set; }

    [NotNull]
    public UserIdentityDto? GridAccessProviderUserIdentity { get; private set; }

    public async Task InitializeAsync()
    {
        EnergySupplierActorIdentity = new ActorIdentityDto(
            ActorNumber.Create(Configuration.EnergySupplierActorNumber),
            ActorRole.EnergySupplier);

        EnergySupplierUserIdentity = new UserIdentityDto(
            UserId: _subsystemTestUserId,
            ActorNumber: EnergySupplierActorIdentity.ActorNumber,
            ActorRole: EnergySupplierActorIdentity.ActorRole);

        GridAccessProviderActorIdentity = new ActorIdentityDto(
            ActorNumber.Create(Configuration.GridAccessProviderActorNumber),
            ActorRole.GridAccessProvider);

        GridAccessProviderUserIdentity = new UserIdentityDto(
            UserId: _subsystemTestUserId,
            ActorNumber: GridAccessProviderActorIdentity.ActorNumber,
            ActorRole: GridAccessProviderActorIdentity.ActorRole);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        Logger.TestOutputHelper = testOutputHelper;
    }

    private IServiceCollection BuildServices()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            // Message client options
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.StartTopicName)}",
                Configuration.ProcessManagerStartTopicName
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.NotifyTopicName)}",
                Configuration.ProcessManagerNotifyTopicName
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataStartTopicName)}",
                Configuration.ProcessManagerBrs021StartTopicName
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataNotifyTopicName)}",
                Configuration.ProcessManagerBrs021NotifyTopicName
            },
            // HTTP client options
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}",
                Configuration.ProcessManagerApplicationIdUri
            },
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}",
                Configuration.ProcessManagerGeneralApiBaseAddress
            },
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}",
                Configuration.ProcessManagerOrchestrationsApiBaseAddress
            },
        });

        serviceCollection.AddAzureClients(b =>
        {
            // Add service bus client for Process Manager message client
            b.AddServiceBusClientWithNamespace(Configuration.ServiceBusNamespace);

            // Add event hub producer client to fake messages from Measurements to Process Manager
            b.AddEventHubProducerClientWithNamespace(Configuration.EventHubNamespace, Configuration.ProcessManagerEventHubName)
                .WithName(ProcessManagerEventHubProducerClientName);
            // b.AddClient<EventHubProducerClient, EventHubProducerClientOptions>(
            //         (_, _, provider) =>
            //         {
            //             var azureCredential = new DefaultAzureCredential();
            //             return new EventHubProducerClient(Configuration.EventHubNamespace, Configuration.ProcessManagerEventHubName, azureCredential);
            //         })
            //     .WithName(ProcessManagerEventHubClientName);
        });

        serviceCollection.AddProcessManagerMessageClient();
        serviceCollection.AddProcessManagerHttpClients();

        return serviceCollection;
    }
}
