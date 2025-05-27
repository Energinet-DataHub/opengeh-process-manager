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

using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_024.V1;

[ParallelWorkflow(WorkflowBucket.Bucket03)] //TODO: WHAT IS THIS? How do I determine the bucket?
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
            // Process Manager HTTP client
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                = SubsystemAuthenticationOptionsForTests.ApplicationIdUri,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = _fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = _fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),

            // Process Manager message client
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.StartTopicName)}"]
                = _fixture.OrchestrationsAppManager.ProcessManagerStartTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.NotifyTopicName)}"]
                = _fixture.ProcessManagerAppManager.ProcessManagerNotifyTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataStartTopicName)}"]
                = _fixture.OrchestrationsAppManager.Brs021ForwardMeteredDataStartTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataNotifyTopicName)}"]
                = _fixture.OrchestrationsAppManager.Brs021ForwardMeteredDataNotifyTopic.Name,
        });
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(_fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerMessageClient();
        services.AddProcessManagerHttpClients();
        ServiceProvider = services.BuildServiceProvider();
    }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        _fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        _fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();
        _fixture.EnqueueBrs026ServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);
        _fixture.EnqueueBrs026ServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        await ServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task
        Given_ValidRequestYearlyMeasurements_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new orchestration instance
        var requestCommand = GivenCommand();

        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(
            requestCommand,
            CancellationToken.None);

        // Step 2a: Query until waiting for EnqueueActorMessagesCompleted notify event
        var (isWaitingForNotify, orchestrationInstance) = await processManagerClient
            .WaitForStepToBeRunning<RequestYearlyMeasurementsInputV1>(
                requestCommand.IdempotencyKey,
                EnqueueActorMessagesStep.StepSequence);

        isWaitingForNotify.Should()
            .BeTrue("because the orchestration instance should wait for a EnqueueActorMessagesCompleted notify event");

                // Step 2b: Verify an enqueue actor messages event is sent on the service bus
        var verifyEnqueueActorMessagesEvent = await _fixture.EnqueueBrs024ServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_024.Name, out var enqueueActorMessagesV1))
                        return false;

                    var requestAcceptedV1 = enqueueActorMessagesV1.ParseData<RequestYearlyMeasurementsAcceptedV1>();

                    return requestAcceptedV1.OriginalTransactionId == requestCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue($"because a {nameof(RequestYearlyMeasurementsAcceptedV1)} service bus message should have been sent");

        // Step 3: Send EnqueueActorMessagesCompleted event
        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new RequestYearlyMeasurementsNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstance!.Id.ToString()),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (orchestrationTerminated, terminatedOrchestrationInstance) = await processManagerClient
            .WaitForOrchestrationInstanceTerminated<RequestYearlyMeasurementsInputV1>(
                requestCommand.IdempotencyKey);

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
                        .And.Be(StepInstanceTerminationState.Succeeded);
                });
    }

    private RequestYearlyMeasurementsCommandV1 GivenCommand()
    {
        const string energySupplierNumber = "1234567891234";
        var energySupplierRole = ActorRole.EnergySupplier.Name;

        var input = new RequestYearlyMeasurementsInputV1(
            ActorMessageId: Guid.NewGuid().ToString(),
            TransactionId: Guid.NewGuid().ToString(),
            ActorNumber: energySupplierNumber,
            ActorRole: energySupplierRole,
            BusinessReason: BusinessReason.PeriodicMetering.Name,
            ReceivedAt: "2024-04-07T22:00:00Z",
            MeteringPointId: "123456789012345678");

        return new RequestYearlyMeasurementsCommandV1(
            OperatingIdentity: _fixture.DefaultActorIdentity,
            InputParameter: input,
            IdempotencyKey: Guid.NewGuid().ToString());
    }
}
