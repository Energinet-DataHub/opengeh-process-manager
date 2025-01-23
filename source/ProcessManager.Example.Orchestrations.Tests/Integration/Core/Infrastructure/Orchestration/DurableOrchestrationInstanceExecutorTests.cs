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

using DurableTask.Core.Exceptions;
using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.NoInputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Core.Infrastructure.Orchestration;

[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class DurableOrchestrationInstanceExecutorTests : IAsyncLifetime
{
    public DurableOrchestrationInstanceExecutorTests(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
        });
        services.AddProcessManagerHttpClients();
        ServiceProvider = services.BuildServiceProvider();
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.ExampleOrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// A test to prove that Durable Client throws an exception if we try to start
    /// an orchestration reusing an instance ID of an already running orchestration.
    ///
    /// We used this knowledge to design idempotency in the DurableOrchestrationInstanceExecutor.
    /// </summary>
    [Fact]
    public async Task Given_StartNewAsync_When_ReusingIdOfRunningInstance_Then_ThrowsExpectedException()
    {
        var instanceId = Guid.NewGuid().ToString();
        var act = () => Fixture.DurableClient.StartNewAsync("Orchestration_Brs_X01_NoInputExample_V1", instanceId);

        await act();

        await act.Should().ThrowAsync<OrchestrationAlreadyExistsException>();
    }

    /// <summary>
    /// A test to prove that Durable Client can start an orchestration if we start
    /// it using an instance ID of an already completed orchestration.
    ///
    /// We used this knowledge to design idempotency in the DurableOrchestrationInstanceExecutor.
    /// </summary>
    [Fact]
    public async Task Given_StartNewAsync_When_UsingIdOfAlreadyCompletedOrchestration_Then_OrchestrationIsStarted()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Start new orchestration instance
        // => Must be done using Process Manager to get the database correct and allow the orchestration to actually complete
        var orchestrationInstanceId = await processManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartNoInputExampleCommandV1(userIdentity),
                CancellationToken.None);

        // => Wait for completion
        var completeOrchestrationStatus = await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationInstanceId.ToString(),
            TimeSpan.FromSeconds(20));

        // Start using Durable Client and already used instance ID
        await Fixture.DurableClient.StartNewAsync(
            "Orchestration_Brs_X01_NoInputExample_V1",
            orchestrationInstanceId.ToString());
    }
}
