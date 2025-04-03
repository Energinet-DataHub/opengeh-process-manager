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
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;

public class ProcessManagerFixture<TConfiguration> : IAsyncLifetime
{
    private readonly Guid _subsystemTestUserId = Guid.Parse("00000000-0000-0000-0000-000000000999");

    private readonly ServiceProvider _services;

    public ProcessManagerFixture()
    {
        Logger = new TestDiagnosticsLogger();

        var serviceCollection = BuildServices();
        _services = serviceCollection.BuildServiceProvider();

        // var configuration = _services.GetRequiredService<IConfiguration>();

        TestConfiguration = default;
    }

    public TestDiagnosticsLogger Logger { get; }

    [NotNull]
    public TConfiguration? TestConfiguration { get; set; }

    public IProcessManagerMessageClient ProcessManagerMessageClient => _services.GetRequiredService<IProcessManagerMessageClient>();

    public IProcessManagerClient ProcessManagerHttpClient => _services.GetRequiredService<IProcessManagerClient>();

    [NotNull]
    public ActorIdentityDto? EnergySupplierActorIdentity { get; private set; }

    [NotNull]
    public UserIdentityDto? UserIdentity { get; private set; }

    public async Task InitializeAsync()
    {
        EnergySupplierActorIdentity = new ActorIdentityDto(
            ActorNumber.Create("1234567890123"), // TODO: Get actor number from app settings
            ActorRole.EnergySupplier);

        UserIdentity = new UserIdentityDto(
            UserId: _subsystemTestUserId, // TODO: Get from app settings or use hardcoded value?
            ActorNumber: EnergySupplierActorIdentity.ActorNumber,
            ActorRole: EnergySupplierActorIdentity.ActorRole);

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

        // TODO: Get settings from app settings
        serviceCollection.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            // Message client options
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}__{nameof(ProcessManagerServiceBusClientOptions.StartTopicName)}",
                "test"
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}__{nameof(ProcessManagerServiceBusClientOptions.NotifyTopicName)}",
                "test"
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}__{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataStartTopicName)}",
                "test"
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}__{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataNotifyTopicName)}",
                "test"
            },
            // HTTP client options
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}__{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}",
                "test"
            },
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}__{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}",
                "test"
            },
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}__{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}",
                "test"
            },
        });

        serviceCollection.AddProcessManagerMessageClient();
        serviceCollection.AddProcessManagerHttpClients();

        return serviceCollection;
    }
}
