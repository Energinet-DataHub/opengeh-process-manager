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

using Energinet.DataHub.Core.TestCommon.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures.Extensions;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.Shared;

public abstract class CalculationScenario
{
    protected CalculationScenario(ProcessManagerFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    protected ProcessManagerFixture Fixture { get; }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.SetTestOutputHelper(null);
        return Task.CompletedTask;
    }

    [SubsystemFact]
    [ScenarioStep(1)]
    public async Task Given_ValidStartCommand()
    {
        // Warm up SQL warehouse, so it is ready for the sql queries at the end of the orchestration
        await Fixture.StartDatabricksSqlWarehouseAsync();
    }

    [SubsystemFact]
    [ScenarioStep(2)]
    public async Task AndGiven_StartNewOrchestrationInstanceIsSent2()
    {
        var orchestrationInstanceId = await Fixture.ProcessManagerHttpClient.StartNewOrchestrationInstanceAsync(
            Fixture.TestConfiguration.StartCommand,
            CancellationToken.None);

        var localTestConfiguration = Fixture.TestConfiguration;
        localTestConfiguration.OrchestrationInstanceId = orchestrationInstanceId;
        Fixture.TestConfiguration = localTestConfiguration;
    }

    [SubsystemFact]
    [ScenarioStep(3)]
    public async Task When_OrchestrationInstanceIsRunning2()
    {
        Assert.True(Fixture.TestConfiguration.OrchestrationInstanceId != Guid.Empty, "If orchestration instance id wasn't set earlier, end tests early.");

        var (success, orchestrationInstance, _) = await Fixture.WaitForOrchestrationInstanceByIdAsync(
            orchestrationInstanceId: Fixture.TestConfiguration.OrchestrationInstanceId,
            orchestrationInstanceState: OrchestrationInstanceLifecycleState.Running);

        Assert.Multiple(
            () => Assert.True(
                success,
                $"An orchestration instance for id \"{Fixture.TestConfiguration.OrchestrationInstanceId}\" should be running."),
            () => Assert.NotNull(orchestrationInstance));

        UpdateOrchestrationInstanceOnFixture(orchestrationInstance!);
    }

    [SubsystemFact]
    [ScenarioStep(4)]
    public async Task Then_OrchestrationInstanceIsTerminatedWithSuccess2()
    {
        Assert.True(Fixture.TestConfiguration.OrchestrationInstanceId != Guid.Empty, "If orchestration instance id wasn't set earlier, end tests early.");

        // Wait up to 30 minutes for the orchestration instance to be terminated. If the databricks warehouse
        // isn't currently running, it takes 5-20 minutes before the databricks query actually starts running.
        var (success, orchestrationInstance, _) = await Fixture.WaitForOrchestrationInstanceByIdAsync(
            orchestrationInstanceId: Fixture.TestConfiguration.OrchestrationInstanceId,
            orchestrationInstanceState: OrchestrationInstanceLifecycleState.Terminated,
            timeoutInMinutes: 30);

        UpdateOrchestrationInstanceOnFixture(orchestrationInstance!);

        Assert.Multiple(
            () => Assert.True(success, "The orchestration instance should be terminated"),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, orchestrationInstance?.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, orchestrationInstance?.Lifecycle.TerminationState));
    }

    private void UpdateOrchestrationInstanceOnFixture(OrchestrationInstanceTypedDto orchestrationInstance)
    {
        var localTestConfiguration = Fixture.TestConfiguration;
        localTestConfiguration.OrchestrationInstance = orchestrationInstance;
        Fixture.TestConfiguration = localTestConfiguration;
    }
}
