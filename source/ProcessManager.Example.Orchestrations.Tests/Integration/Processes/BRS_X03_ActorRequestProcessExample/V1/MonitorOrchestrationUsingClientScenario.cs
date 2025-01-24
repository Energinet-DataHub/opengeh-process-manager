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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_X03_ActorRequestProcessExample.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to notify an example orchestration
/// and monitor its status during its lifetime.
/// </summary>
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
        Fixture.ExampleConsumerAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.SetTestOutputHelper(null);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Tests that we can send a notify event using the <see cref="IProcessManagerMessageClient"/>.
    /// The test performs the following orchestration, by running both an Orchestrations app and a Consumer app:
    /// 1. Start BRX-X03 (ActorRequestProcessExample) from consumer app.
    /// 2. Receive start orchestration service bus message and start the orchestration in Orchestrations app.
    /// 3. Send EnqueueActorMessages event from orchestration to the Consumer app, and wait for ActorMessagesEnqueued notify.
    /// 4. Receive EnqueueActorMessages event in Consumer app, and send ActorMessagesEnqueued notify event back.
    /// 5. Terminate orchestration (with TerminationState=Succeeded) in Orchestrations app, if ActorMessagesEnqueued event is received before timeout.
    /// </summary>
    [Fact]
    public async Task Given_ActorRequestProcessExampleOrchestration_AndGiven_Consumer_When_StartedByConsumer_Then_OrchestrationTerminatesSuccessfully()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new BRS-X03 using the Example.Consumer app
        var idempotencyKey = Guid.NewGuid().ToString();
        await Fixture.ExampleConsumerAppManager.AppHostManager.HttpClient.PostAsJsonAsync(
            requestUri: "/api/actor-request-process/start",
            value: idempotencyKey);

        // Step 2: Query until terminated with succeeded
        var (isTerminated, succeededOrchestrationInstance) = await processManagerClient
            .TryWaitForOrchestrationInstance<NotifyOrchestrationInstanceExampleInputV1>(
                idempotencyKey: idempotencyKey,
                (oi) => oi is
                {
                    Lifecycle:
                    {
                        State: OrchestrationInstanceLifecycleState.Terminated,
                        TerminationState: OrchestrationInstanceTerminationState.Succeeded,
                    },
                });

        isTerminated.Should().BeTrue("because the BRS-X03 orchestration instance should complete within given wait time");
        succeededOrchestrationInstance.Should().NotBeNull();

        succeededOrchestrationInstance!.Steps.Should()
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
