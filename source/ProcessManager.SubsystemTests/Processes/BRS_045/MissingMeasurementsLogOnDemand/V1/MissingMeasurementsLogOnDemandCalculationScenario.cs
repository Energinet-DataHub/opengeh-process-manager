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
using Energinet.DataHub.Core.TestCommon.Xunit.Orderers;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures.Extensions;
using NodaTime;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_045.MissingMeasurementsLogOnDemand.V1;

[TestCaseOrderer(
    ordererTypeName: TestCaseOrdererLocation.OrdererTypeName,
    ordererAssemblyName: TestCaseOrdererLocation.OrdererAssemblyName)]
public class MissingMeasurementsLogOnDemandCalculationScenario
    : IClassFixture<ProcessManagerFixture<MissingMeasurementsLogOnDemandCalculationScenarioState>>,
    IAsyncLifetime
{
    private readonly ProcessManagerFixture<MissingMeasurementsLogOnDemandCalculationScenarioState> _fixture;
    private readonly IClock _clock;

    public MissingMeasurementsLogOnDemandCalculationScenario(
        ProcessManagerFixture<MissingMeasurementsLogOnDemandCalculationScenarioState> fixture,
        ITestOutputHelper testOutputHelper,
        IClock clock)
    {
        _fixture = fixture;
        _clock = clock;
        _fixture.SetTestOutputHelper(testOutputHelper);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _fixture.SetTestOutputHelper(null);
        return Task.CompletedTask;
    }

    [SubsystemFact]
    [ScenarioStep(1)]
    public async Task Given_ValidStartElectricalHeatingCalculationCommand()
    {
        // Warm up SQL warehouse, so it is ready for the sql queries at the end of the orchestration
        await _fixture.StartDatabricksSqlWarehouseAsync();
        var periodStart = _clock.GetCurrentInstant().ToDateTimeOffset();
        var periodEnd = periodStart.AddDays(1);

        _fixture.TestConfiguration = new MissingMeasurementsLogOnDemandCalculationScenarioState(
            startCommand: new StartCalculationCommandV1(_fixture.UserIdentity, new CalculationInputV1(periodStart, periodEnd, ["301"])));
    }

    [SubsystemFact]
    [ScenarioStep(2)]
    public async Task AndGiven_StartNewOrchestrationInstanceIsSent()
    {
        var orchestrationInstanceId = await _fixture.ProcessManagerHttpClient.StartNewOrchestrationInstanceAsync(
            _fixture.TestConfiguration.StartCommand,
            CancellationToken.None);

        _fixture.TestConfiguration.OrchestrationInstanceId = orchestrationInstanceId;
    }

    [SubsystemFact]
    [ScenarioStep(3)]
    public async Task When_OrchestrationInstanceIsRunning()
    {
        Assert.True(_fixture.TestConfiguration.OrchestrationInstanceId != Guid.Empty, "If orchestration instance id wasn't set earlier, end tests early.");

        var (success, orchestrationInstance, _) = await _fixture.WaitForOrchestrationInstanceByIdAsync(
            orchestrationInstanceId: _fixture.TestConfiguration.OrchestrationInstanceId,
            orchestrationInstanceState: OrchestrationInstanceLifecycleState.Running);

        Assert.Multiple(
            () => Assert.True(
                success,
                $"An orchestration instance for id \"{_fixture.TestConfiguration.OrchestrationInstanceId}\" should be running."),
            () => Assert.NotNull(orchestrationInstance));

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;
    }

    [SubsystemFact]
    [ScenarioStep(4)]
    public async Task Then_OrchestrationInstanceIsTerminatedWithSuccess()
    {
        Assert.True(_fixture.TestConfiguration.OrchestrationInstanceId != Guid.Empty, "If orchestration instance id wasn't set earlier, end tests early.");

        // Wait up to 30 minutes for the orchestration instance to be terminated. If the databricks warehouse
        // isn't currently running, it takes 5-20 minutes before the databricks query actually starts running.
        var (success, orchestrationInstance, _) = await _fixture.WaitForOrchestrationInstanceByIdAsync(
                orchestrationInstanceId: _fixture.TestConfiguration.OrchestrationInstanceId,
                orchestrationInstanceState: OrchestrationInstanceLifecycleState.Terminated,
                timeoutInMinutes: 30);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "The orchestration instance should be terminated"),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, orchestrationInstance?.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, orchestrationInstance?.Lifecycle.TerminationState));
    }
}
