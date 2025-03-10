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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;

public class ProcessManagerFixture : IAsyncLifetime
{
    private readonly ServiceProvider _services;

    public ProcessManagerFixture()
    {
        var serviceCollection = BuildServices();
        _services = serviceCollection.BuildServiceProvider();
    }

    public ITestConfiguration? TestConfiguration { get; set; }

    [NotNull]
    public ActorIdentityDto? EnergySupplierActorIdentity { get; private set; }

    public IProcessManagerMessageClient ProcessManagerMessageClient => _services.GetRequiredService<IProcessManagerMessageClient>();

    public async Task InitializeAsync()
    {
        EnergySupplierActorIdentity = new ActorIdentityDto(
            ActorNumber.Create("1234567890123"), // TODO: Get actor number from app settings
            ActorRole.EnergySupplier);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    private IServiceCollection BuildServices()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
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
        });

        serviceCollection.AddProcessManagerMessageClient();

        return serviceCollection;
    }
}
