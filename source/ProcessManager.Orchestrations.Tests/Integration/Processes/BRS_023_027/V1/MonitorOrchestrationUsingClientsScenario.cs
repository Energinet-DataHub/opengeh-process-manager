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
using Energinet.DataHub.Core.FunctionApp.TestCommon.FunctionAppHost;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Azure.Databricks.Client.Models;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Proto = Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Contracts;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_023_027.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to start a
/// calculation orchestration (with input parameter) and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private const string CalculationJobName = "CalculatorJob";

    public MonitorOrchestrationUsingClientsScenario(
        OrchestrationsAppFixture fixture,
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
                = Fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"]
                = Fixture.ProcessManagerTopicName,
        });
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(Fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerHttpClients();
        services.AddProcessManagerMessageClient();
        ServiceProvider = services.BuildServiceProvider();

        ProcessManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
        ProcessManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
    }

    private OrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    private IProcessManagerClient ProcessManagerClient { get;  }

    private IProcessManagerMessageClient ProcessManagerMessageClient { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        Fixture.OrchestrationsAppManager.EnsureAppHostUsesMockedDatabricksApi(true);

        Fixture.EnqueueBrs023027ServiceBusListener.ResetMessageHandlersAndReceivedMessages();
        Fixture.IntegrationEventServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Calculation_WhenStarted_CanMonitorLifecycle()
    {
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        var calculationType = CalculationType.WholesaleFixing;
        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Step 1: Start new calculation orchestration instance
        var inputParameter = new CalculationInputV1(
            calculationType,
            GridAreaCodes: new[] { "999" },
            PeriodStartDate: new DateTimeOffset(2023, 1, 31, 23, 0, 0, TimeSpan.Zero),
            PeriodEndDate: new DateTimeOffset(2023, 2, 28, 23, 0, 0, TimeSpan.Zero),
            IsInternalCalculation: false);
        var orchestrationInstanceId = await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartCalculationCommandV1(
                    userIdentity,
                    inputParameter),
                CancellationToken.None);

        // Step 2.0: Wait for service bus message to EDI and mock a response
        await Fixture.EnqueueBrs023027ServiceBusListener.WaitAndMockServiceBusMessageToAndFromEdi(
            ProcessManagerMessageClient,
            orchestrationInstanceId);

        // step 2.5: Wait for the integration event to be published
        await Fixture.IntegrationEventServiceBusListener.WaitAndAssertCalculationEnqueueCompletedIntegrationEvent(
            orchestrationInstanceId: orchestrationInstanceId,
            calculationType: Proto.CalculationType.WholesaleFixing);

        // Step 3: Query until terminated with succeeded
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await ProcessManagerClient
                    .GetOrchestrationInstanceByIdAsync<CalculationInputV1>(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationState.Succeeded;
            },
            timeLimit: TimeSpan.FromSeconds(20),
            delay: TimeSpan.FromSeconds(3));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");

        // Step 4: General search using name and termination state
        var orchestrationInstancesGeneralSearch = await ProcessManagerClient
            .SearchOrchestrationInstancesByNameAsync<CalculationInputV1>(
                new SearchOrchestrationInstancesByNameQuery(
                    userIdentity,
                    name: Brs_023_027.Name,
                    version: null,
                    lifecycleState: OrchestrationInstanceLifecycleState.Terminated,
                    terminationState: OrchestrationInstanceTerminationState.Succeeded,
                    startedAtOrLater: null,
                    terminatedAtOrEarlier: null),
                CancellationToken.None);

        orchestrationInstancesGeneralSearch.Should().Contain(x => x.Id == orchestrationInstanceId);

        // Step 5: Custom search
        var customQuery = new CalculationQuery(userIdentity)
        {
            CalculationTypes = new[] { inputParameter.CalculationType },
            GridAreaCodes = inputParameter.GridAreaCodes,
            PeriodStartDate = inputParameter.PeriodStartDate,
            PeriodEndDate = inputParameter.PeriodEndDate,
            IsInternalCalculation = inputParameter.IsInternalCalculation,
        };
        var orchestrationInstancesCustomSearch = await ProcessManagerClient
            .SearchOrchestrationInstancesByCustomQueryAsync(
                customQuery,
                CancellationToken.None);

        orchestrationInstancesCustomSearch.Should().Contain(x => x.OrchestrationInstance.Id == orchestrationInstanceId);

        // TODO: Enable when custom filtering has been implemented correct
        orchestrationInstancesCustomSearch.Count.Should().Be(1);
    }

    [Fact]
    public async Task Calculation_WhenScheduledToRunInThePast_CanMonitorLifecycle()
    {
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Step 1: Schedule new calculation orchestration instance
        var orchestrationInstanceId = await ProcessManagerClient
            .ScheduleNewOrchestrationInstanceAsync(
                new ScheduleCalculationCommandV1(
                    userIdentity,
                    runAt: DateTimeOffset.Parse("2024-11-01T06:19:10.0209567+01:00"),
                    inputParameter: new CalculationInputV1(
                        CalculationType.BalanceFixing,
                        GridAreaCodes: new[] { "543" },
                        PeriodStartDate: new DateTimeOffset(2022, 1, 11, 23, 0, 0, TimeSpan.Zero),
                        PeriodEndDate: new DateTimeOffset(2022, 1, 12, 23, 0, 0, TimeSpan.Zero),
                        IsInternalCalculation: false)),
                CancellationToken.None);

        // Step 2: Trigger the scheduler to queue the calculation orchestration instance
        await Fixture.ProcessManagerAppManager.AppHostManager
            .TriggerFunctionAsync("StartScheduledOrchestrationInstances");

        // Step 3.0: Wait for service bus message to EDI and mock a response
        await Fixture.EnqueueBrs023027ServiceBusListener.WaitAndMockServiceBusMessageToAndFromEdi(
            ProcessManagerMessageClient,
            orchestrationInstanceId);

        // step 3.5: Wait for the integration event to be published
        await Fixture.IntegrationEventServiceBusListener.WaitAndAssertCalculationEnqueueCompletedIntegrationEvent(
            orchestrationInstanceId: orchestrationInstanceId,
            calculationType: Proto.CalculationType.BalanceFixing);

        // Step 4: Query until terminated with succeeded
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await ProcessManagerClient
                    .GetOrchestrationInstanceByIdAsync<CalculationInputV1>(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationState.Succeeded;
            },
            timeLimit: TimeSpan.FromSeconds(20),
            delay: TimeSpan.FromSeconds(3));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }

    [Fact]
    public async Task CalculationScheduledToRunInTheFuture_WhenCanceled_CanMonitorLifecycle()
    {
        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Step 1: Schedule new calculation orchestration instance
        var orchestrationInstanceId = await ProcessManagerClient
            .ScheduleNewOrchestrationInstanceAsync(
                new ScheduleCalculationCommandV1(
                    userIdentity,
                    runAt: DateTimeOffset.Parse("2050-01-01T12:00:00.0000000+01:00"),
                    inputParameter: new CalculationInputV1(
                        CalculationType.Aggregation,
                        GridAreaCodes: new[] { "543" },
                        PeriodStartDate: new DateTimeOffset(2022, 1, 11, 23, 0, 0, TimeSpan.Zero),
                        PeriodEndDate: new DateTimeOffset(2022, 1, 12, 23, 0, 0, TimeSpan.Zero),
                        IsInternalCalculation: true)),
                CancellationToken.None);

        // Step 2: Cancel the calculation orchestration instance
        await ProcessManagerClient
            .CancelScheduledOrchestrationInstanceAsync(
                new CancelScheduledOrchestrationInstanceCommand(
                    userIdentity,
                    orchestrationInstanceId),
                CancellationToken.None);

        // Step 3: Query until terminated with user canceled
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await ProcessManagerClient
                    .GetOrchestrationInstanceByIdAsync<CalculationInputV1>(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationState.UserCanceled;
            },
            timeLimit: TimeSpan.FromSeconds(60),
            delay: TimeSpan.FromSeconds(3));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }

    [Fact]
    public async Task Calculation_WhenEdiDoesNotRespond_ThenOrchestrationTimesOutAndHasStatusFailed()
    {
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Step 1: Start new calculation orchestration instance
        var inputParameter = new CalculationInputV1(
            CalculationType.WholesaleFixing,
            GridAreaCodes: new[] { "804" },
            PeriodStartDate: new DateTimeOffset(2023, 1, 31, 23, 0, 0, TimeSpan.Zero),
            PeriodEndDate: new DateTimeOffset(2023, 2, 28, 23, 0, 0, TimeSpan.Zero),
            IsInternalCalculation: false);

        var orchestrationInstanceId = await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartCalculationCommandV1(
                    userIdentity,
                    inputParameter),
                CancellationToken.None);

        // Step 2: Query until terminated with failed
        var isTerminatedWithFailed = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await ProcessManagerClient
                    .GetOrchestrationInstanceByIdAsync<CalculationInputV1>(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationState.Failed;
            },
            timeLimit: TimeSpan.FromSeconds(30),
            delay: TimeSpan.FromSeconds(3));

        isTerminatedWithFailed.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }
}
