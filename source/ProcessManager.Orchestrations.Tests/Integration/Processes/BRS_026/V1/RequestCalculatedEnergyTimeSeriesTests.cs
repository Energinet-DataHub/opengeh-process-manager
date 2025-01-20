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

using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_026.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// request calculated energy time series orchestration and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class RequestCalculatedEnergyTimeSeriesTests : IAsyncLifetime
{
    private readonly OrchestrationsAppFixture _fixture;

    public RequestCalculatedEnergyTimeSeriesTests(
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

    [Fact]
    public async Task RequestCalculatedEnergyTimeSeries_WhenStarted_OrchestrationCompletesWithSuccess()
    {
        // Given
        var businessReason = "BalanceFixing";
        var energySupplierNumber = "23143245321";
        var startRequestCommand = new RequestCalculatedEnergyTimeSeriesCommandV1(
            new ActorIdentityDto(Guid.NewGuid()),
            new RequestCalculatedEnergyTimeSeriesInputV1(
                RequestedForActorNumber: energySupplierNumber,
                RequestedForActorRole: "EnergySupplier",
                BusinessReason: businessReason,
                PeriodStart: "2024-04-07 23:00:00",
                PeriodEnd: "2024-04-08 23:00:00",
                EnergySupplierNumber: energySupplierNumber,
                BalanceResponsibleNumber: null,
                GridAreas: ["804"],
                MeteringPointType: null,
                SettlementMethod: null,
                SettlementVersion: null),
            idempotencyKey: Guid.NewGuid().ToString());

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();

        // When
        var orchestrationCreatedAfter = DateTime.UtcNow.AddSeconds(-1);
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(startRequestCommand, CancellationToken.None);

        // Then
        // => Orchestration is started
        var orchestration = await _fixture.DurableClient.WaitForOrchestationStartedAsync(
            createdTimeFrom: orchestrationCreatedAfter,
            name: nameof(Orchestration_Brs_026_V1));
        orchestration.Input.ToString().Should().Contain(businessReason);

        // => Orchestration is waiting for notify event
        // Using JToken instead of string, since there is a timing where the custom status is not set, so casting to string fails. TODO: Fix by looking at step status instead.
        await _fixture.DurableClient.WaitForCustomStatusAsync<JToken>(
            orchestration.InstanceId,
            (customStatus) => customStatus.ToString() == Orchestration_Brs_026_V1.CustomStatus.WaitingForEnqueueActorMessages);

        // => Send notify event
        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent(
                orchestration.InstanceId,
                RequestCalculatedEnergyTimeSeriesNotifyEventsV1.EnqueueActorMessagesCompleted),
            CancellationToken.None);

        // => Orchestration is completed (with success)
        var completedOrchestration = await _fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestration.InstanceId);

        using var assertionScope = new AssertionScope();
        completedOrchestration.RuntimeStatus.Should().Be(OrchestrationRuntimeStatus.Completed);
        completedOrchestration.Output.ToString().Should().Contain("Success");
    }

    /// <summary>
    /// Showing how we can orchestrate and monitor an orchestration instance only using clients.
    /// </summary>
    [Fact]
    public async Task RequestCalculatedEnergyTimeSeries_WhenStarted_CanMonitorLifecycle()
    {
        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Step 1: Start new orchestration instance
        var businessReason = "BalanceFixing";
        var energySupplierNumber = "23143245321";
        var startRequestCommand = new RequestCalculatedEnergyTimeSeriesCommandV1(
            new ActorIdentityDto(Guid.NewGuid()),
            new RequestCalculatedEnergyTimeSeriesInputV1(
                RequestedForActorNumber: energySupplierNumber,
                RequestedForActorRole: "EnergySupplier",
                BusinessReason: businessReason,
                PeriodStart: "2024-04-07 23:00:00",
                PeriodEnd: "2024-04-08 23:00:00",
                EnergySupplierNumber: energySupplierNumber,
                BalanceResponsibleNumber: null,
                GridAreas: ["804"],
                MeteringPointType: null,
                SettlementMethod: null,
                SettlementVersion: null),
            idempotencyKey: Guid.NewGuid().ToString());

        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startRequestCommand,
            CancellationToken.None);

        // Step 2: Query until waiting for EnqueueActorMessagesCompleted notify event
        var (isWaitingForNotify, orchestrationInstance) = await processManagerClient
            .TryWaitForOrchestrationInstance<RequestCalculatedEnergyTimeSeriesInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                (oi) =>
                {
                    var enqueueActorMessagesStep = oi.Steps
                        .Single(s => s.Sequence == Orchestration_Brs_026_V1.EnqueueActorMessagesStepSequence);

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
                EventName: RequestCalculatedEnergyTimeSeriesNotifyEventsV1.EnqueueActorMessagesCompleted),
            CancellationToken.None);

        // Step 4: Query until terminated with succeeded
        var (isTerminated, _) = await processManagerClient
            .TryWaitForOrchestrationInstance<RequestCalculatedEnergyTimeSeriesInputV1>(
                idempotencyKey: startRequestCommand.IdempotencyKey,
                (oi) => oi is
                {
                    Lifecycle:
                    {
                        State: OrchestrationInstanceLifecycleState.Terminated,
                        TerminationState: OrchestrationInstanceTerminationState.Succeeded,
                    },
                });

        isTerminated.Should().BeTrue("because the orchestration instance should complete within given wait time");
    }
}
