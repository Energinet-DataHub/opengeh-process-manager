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

using System.Text.Json;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Example.Consumer.Functions.BRS_X03_ActorRequestProcessExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1.BusinessValidation.ValidationRules;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
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
    public async Task Given_ConsumerApp_When_BRS_X03_OrchestrationInstanceStartedByConsumer_Then_OrchestrationTerminatesSuccessfully()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new BRS-X03 using the Example.Consumer app
        var idempotencyKey = Guid.NewGuid().ToString();
        var startTriggerInput = new StartTrigger_Brs_X03.StartTriggerInput(
            IdempotencyKey: idempotencyKey,
            BusinessReason: "B01");
        await Fixture.ExampleConsumerAppManager.AppHostManager.HttpClient.PostAsJsonAsync(
            requestUri: "/api/actor-request-process/start",
            value: startTriggerInput);

        // Step 2: Query until terminated with succeeded
        var (isTerminated, succeededOrchestrationInstance) = await processManagerClient
            .TryWaitForOrchestrationInstance<ActorRequestProcessExampleInputV1>(
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

    /// <summary>
    /// Tests that an orchestration instance is set to failed when the business validation fails.
    /// The test performs the following orchestration, by running both an Orchestrations app and a Consumer app:
    /// 1. Start BRX-X03 (ActorRequestProcessExample) from consumer app with an invalid business reason.
    /// 2. Receive start orchestration service bus message and start the orchestration in Orchestrations app.
    /// 3. Fail the business validation step (and write validation errors to the step's custom state).
    /// 3. Send EnqueueActorMessages (reject) event from orchestration to the Consumer app, and wait for ActorMessagesEnqueued notify.
    /// 4. Receive EnqueueActorMessages event in Consumer app, and send ActorMessagesEnqueued notify event back.
    /// 5. Terminate orchestration (with TerminationState=Failed) in Orchestrations app, because business validation failed.
    /// </summary>
    [Fact]
    public async Task Given_ConsumerApp_AndGiven_InvalidBusinessReasonInput_When_BRS_X03_OrchestrationInstanceStartedByConsumer_Then_OrchestrationTerminatesWithFailed_AndThen_ValidationStepTerminatesWithFailed()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new BRS-X03 using the Example.Consumer app, with an invalid business reason (empty string)
        var idempotencyKey = Guid.NewGuid().ToString();
        var startTriggerInput = new StartTrigger_Brs_X03.StartTriggerInput(
            IdempotencyKey: idempotencyKey,
            BusinessReason: BusinessReasonValidationRule.InvalidBusinessReason);
        await Fixture.ExampleConsumerAppManager.AppHostManager.HttpClient.PostAsJsonAsync(
            requestUri: "/api/actor-request-process/start",
            value: startTriggerInput);

        // Step 2: Query until terminated
        var (isTerminated, orchestrationInstance) = await processManagerClient
            .TryWaitForOrchestrationInstance<ActorRequestProcessExampleInputV1>(
                idempotencyKey: idempotencyKey,
                (oi) => oi is
                {
                    Lifecycle.State: OrchestrationInstanceLifecycleState.Terminated,
                });

        isTerminated.Should().BeTrue("because the BRS-X03 orchestration instance should complete within given wait time");
        orchestrationInstance.Should().NotBeNull();

        using var assertionScope = new AssertionScope();

        orchestrationInstance!.Lifecycle.TerminationState.Should().Be(OrchestrationInstanceTerminationState.Failed);

        orchestrationInstance.Steps.OrderBy(s => s.Sequence).Should()
            .SatisfyRespectively(
                validationStep =>
                {
                    validationStep.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    validationStep.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(OrchestrationStepTerminationState.Failed);
                    JsonSerializer.Deserialize<ValidationError[]>(validationStep.CustomState)
                        .Should()
                        .Satisfy(ve => ve.Message.Equals(BusinessReasonValidationRule.ValidationErrorMessage));
                },
                enqueueStep =>
                {
                    enqueueStep.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    enqueueStep.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(OrchestrationStepTerminationState.Succeeded);
                });
    }
}
