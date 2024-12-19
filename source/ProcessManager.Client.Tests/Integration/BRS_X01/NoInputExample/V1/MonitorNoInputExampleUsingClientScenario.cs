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

using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_X01.NoInputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Client.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Client.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Integration.BRS_X01.NoInputExample.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to start an
/// example orchestration (with no input parameter) and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ProcessManagerClientCollection))]
public class MonitorNoInputExampleUsingClientScenario : IAsyncLifetime
{
    public MonitorNoInputExampleUsingClientScenario(
        ProcessManagerClientFixture fixture,
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

    private ProcessManagerClientFixture Fixture { get; }

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

    [Fact]
    public async Task NoInputExampleOrchestration_WhenStarted_CanMonitorLifecycle()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Step 1: Start new orchestration instance
        var orchestrationInstanceId = await processManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartNoInputExampleComandV1(
                    userIdentity),
                CancellationToken.None);

        // Step 2: Query until terminated with succeeded
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await processManagerClient
                    .GetOrchestrationInstanceByIdAsync(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleStates.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationStates.Succeeded;
            },
            timeLimit: TimeSpan.FromSeconds(60),
            delay: TimeSpan.FromSeconds(3));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");

        // Step 3: General search using name and termination state
        var orchestrationInstancesGeneralSearch = await processManagerClient
            .SearchOrchestrationInstancesByNameAsync(
                new SearchOrchestrationInstancesByNameQuery(
                    userIdentity,
                    name: new Brs_X01_NoInputExample_V1().Name,
                    version: null,
                    lifecycleState: OrchestrationInstanceLifecycleStates.Terminated,
                    terminationState: OrchestrationInstanceTerminationStates.Succeeded,
                    startedAtOrLater: null,
                    terminatedAtOrEarlier: null),
                CancellationToken.None);

        orchestrationInstancesGeneralSearch.Should().Contain(x => x.Id == orchestrationInstanceId);
    }
}
