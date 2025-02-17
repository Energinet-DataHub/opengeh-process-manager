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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.InternalProcesses.MigrateCalculationsFromWholesale.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.InternalProcesses.MigrateCalculationsFromWholesale.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// request calculated energy time series orchestration and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private readonly OrchestrationsAppFixture _fixture;

    public MonitorOrchestrationUsingClientsScenario(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = _fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = _fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
        });
        services.AddProcessManagerHttpClients();
        ServiceProvider = services.BuildServiceProvider();
    }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        _fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        _fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Tests the BRS-026 orchestration instance when the request is valid and actor messages should be enqueued.
    /// </summary>
    [Fact]
    public async Task Given_ValidRequestCalculatedEnergyTimeSeries_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new orchestration instance
        var userIdentityDto = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorNumber: ActorNumber.Create("0000000000000"),
            ActorRole: ActorRole.DataHubAdministrator);

        var orchestrationInstanceId = await processManagerClient.StartNewOrchestrationInstanceAsync(
            new MigrateCalculationsFromWholesaleCommandV1(
                userIdentityDto),
            CancellationToken.None);

        // Step 2: Query until terminated
        OrchestrationInstanceTypedDto? terminatedOrchestrationInstance = null;
        var orchestrationTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                terminatedOrchestrationInstance = await processManagerClient
                    .GetOrchestrationInstanceByIdAsync(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentityDto,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return terminatedOrchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated;
            },
            timeLimit: TimeSpan.FromSeconds(60),
            delay: TimeSpan.FromSeconds(3));

        orchestrationTerminated.Should().BeTrue(
            "because the orchestration instance should be terminated within the given wait time");

        // Orchestration instance and all steps should be Succeeded
        using var assertionScope = new AssertionScope();
        terminatedOrchestrationInstance!.Lifecycle.TerminationState.Should()
            .NotBeNull()
            .And.Be(OrchestrationInstanceTerminationState.Succeeded);

        terminatedOrchestrationInstance.Steps.Should()
            .AllSatisfy(
                s =>
                {
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(OrchestrationStepTerminationState.Succeeded);
                });
    }
}
