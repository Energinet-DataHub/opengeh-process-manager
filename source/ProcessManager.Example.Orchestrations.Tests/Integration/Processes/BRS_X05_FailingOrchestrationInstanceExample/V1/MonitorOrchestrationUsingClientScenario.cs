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
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1.Steps;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1;

[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientScenario : IAsyncLifetime
{
    public MonitorOrchestrationUsingClientScenario(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            // Process Manager HTTP client
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                = Fixture.ProcessManagerAppManager.ApplicationIdUri,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
        });

        // Process Manager HTTP client
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
        Fixture.SetTestOutputHelper(null);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Tests that we set the step and the orchestration instance to failed if an activity fails (even with retries).
    /// </summary>
    [Fact]
    public async Task Given_FailingOrchestrationInstanceActivity_When_OrchestrationInstanceStarted_Then_OrchestrationInstanceTerminatedWithFailed_AndThen_FailingStepTerminatedWithFailed()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new orchestration instance
        var userIdentity = new UserIdentityDto(Guid.NewGuid(), ActorNumber.Create("1234567891234"), ActorRole.EnergySupplier);
        var startRequestCommand = new StartFailingOrchestrationInstanceExampleV1(userIdentity);

        var orchestrationInstanceId = await processManagerClient.StartNewOrchestrationInstanceAsync(
            startRequestCommand,
            CancellationToken.None);

        // Step 2: Query until terminated
        OrchestrationInstanceTypedDto? orchestrationInstance = null;
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                orchestrationInstance = await processManagerClient
                    .GetOrchestrationInstanceByIdAsync(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated;
            },
            timeLimit: TimeSpan.FromSeconds(60),
            delay: TimeSpan.FromMilliseconds(200));

        isTerminated.Should().BeTrue("because the orchestration instance should terminate within given wait time");
        orchestrationInstance.Should().NotBeNull();

        using var assertionScope = new AssertionScope();

        orchestrationInstance!.Lifecycle.TerminationState.Should().Be(
            OrchestrationInstanceTerminationState.Failed,
            "because the orchestration instance should be failed");

        orchestrationInstance.Steps.OrderBy(s => s.Sequence).Should().SatisfyRespectively(
            successStep =>
            {
                successStep.Sequence.Should().Be(SuccessStep.StepSequence);
                successStep.Lifecycle.TerminationState.Should().Be(OrchestrationStepTerminationState.Succeeded);
            },
            failingStep =>
            {
                failingStep.Sequence.Should().Be(FailingStep.StepSequence);
                failingStep.Lifecycle.TerminationState.Should().Be(OrchestrationStepTerminationState.Failed);
                failingStep.CustomState.Should().Contain(typeof(TaskFailedException).FullName);
                failingStep.CustomState.Should().Contain(FailingActivity_Brs_X05_V1.ExceptionMessage);
            });
    }
}
