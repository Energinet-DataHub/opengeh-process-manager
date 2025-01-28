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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_026_028.BRS_028.V1;

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
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"]
                = _fixture.ProcessManagerTopicName,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = _fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = _fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
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

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Showing how we can orchestrate and monitor an orchestration instance only using clients.
    /// </summary>
    [Fact]
    public async Task RequestCalculatedWholesaleServices_WhenStarted_CanMonitorLifecycle()
    {
        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new orchestration instance
        var businessReason = BusinessReason.WholesaleFixing.Name;
        const string energySupplierNumber = "1111111111111";
        var startRequestCommand = new RequestCalculatedWholesaleServicesCommandV1(
            new ActorIdentityDto(Guid.NewGuid()),
            new RequestCalculatedWholesaleServicesInputV1(
                RequestedForActorNumber: energySupplierNumber,
                RequestedForActorRole: ActorRole.EnergySupplier.Name,
                BusinessReason: businessReason,
                PeriodStart: "2024-12-31T23:00:00Z",
                PeriodEnd: "2025-01-31T23:00:00Z",
                Resolution: null,
                EnergySupplierNumber: energySupplierNumber,
                ChargeOwnerNumber: null,
                GridAreas: ["804"],
                SettlementVersion: null,
                ChargeTypes: null),
            idempotencyKey: Guid.NewGuid().ToString());

        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startRequestCommand,
            CancellationToken.None);

        // Step 2: Query until waiting for EnqueueActorMessagesCompleted notify event
        var (isWaitingForNotify, orchestrationInstance) = await processManagerClient
            .TryWaitForOrchestrationInstance<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                comparer: (oi) =>
                {
                    var enqueueActorMessagesStep = oi.Steps
                        .Single(s => s.Sequence == Orchestration_Brs_028_V1.EnqueueActorMessagesStepSequence);

                    return enqueueActorMessagesStep.Lifecycle.State == StepInstanceLifecycleState.Running;
                });

        isWaitingForNotify.Should()
            .BeTrue("because the orchestration instance should wait for a EnqueueActorMessagesCompleted notify event");

        if (orchestrationInstance is null)
            ArgumentNullException.ThrowIfNull(orchestrationInstance, nameof(orchestrationInstance));

        // Step 3: Send EnqueueActorMessagesCompleted event
        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent(
                OrchestrationInstanceId: orchestrationInstance.Id.ToString(),
                EventName: RequestCalculatedWholesaleServicesNotifyEventsV1.EnqueueActorMessagesCompleted),
            CancellationToken.None);

        // Step 4: Query until terminated with succeeded
        var (orchestrationTerminatedWithSucceeded, terminatedOrchestrationInstance) = await processManagerClient
            .TryWaitForOrchestrationInstance<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                (oi) => oi is
                {
                    Lifecycle:
                    {
                        State: OrchestrationInstanceLifecycleState.Terminated,
                        TerminationState: OrchestrationInstanceTerminationState.Succeeded,
                    },
                });

        orchestrationTerminatedWithSucceeded.Should().BeTrue("because the orchestration instance should complete within given wait time");

        // If isTerminated is true then terminatedOrchestrationInstance should never be null
        ArgumentNullException.ThrowIfNull(terminatedOrchestrationInstance);

        // => All steps should be Succeeded
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
