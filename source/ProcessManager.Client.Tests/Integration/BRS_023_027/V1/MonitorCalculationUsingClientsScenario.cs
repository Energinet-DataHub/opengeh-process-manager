﻿// Copyright 2020 Energinet DataHub A/S
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

using Energinet.DataHub.Core.FunctionApp.TestCommon.FunctionAppHost;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Api.Model;
using Energinet.DataHub.ProcessManager.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Client.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Integration.BRS_023_027.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to start a
/// calculation orchestration and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ProcessManagerClientCollection))]
public class MonitorCalculationUsingClientsScenario : IAsyncLifetime
{
    public MonitorCalculationUsingClientsScenario(
        ScenarioProcessManagerAppFixture processManagerAppFixture,
        ScenarioOrchestrationsAppFixture orchestrationsAppFixture,
        ITestOutputHelper testOutputHelper)
    {
        ProcessManagerAppFixture = processManagerAppFixture;
        ProcessManagerAppFixture.SetTestOutputHelper(testOutputHelper);

        OrchestrationsAppFixture = orchestrationsAppFixture;
        OrchestrationsAppFixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddScoped(_ => CreateInMemoryConfigurations(new Dictionary<string, string?>()
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = ProcessManagerAppFixture.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = OrchestrationsAppFixture.AppHostManager.HttpClient.BaseAddress!.ToString(),
        }));
        services.AddProcessManagerHttpClients();
        ServiceProvider = services.BuildServiceProvider();
    }

    private ScenarioProcessManagerAppFixture ProcessManagerAppFixture { get; }

    private ScenarioOrchestrationsAppFixture OrchestrationsAppFixture { get; }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        ProcessManagerAppFixture.AppHostManager.ClearHostLog();
        OrchestrationsAppFixture.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        ProcessManagerAppFixture.SetTestOutputHelper(null!);
        OrchestrationsAppFixture.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Calculation_WhenStarted_CanMonitorLifecycle()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Step 1: Start new calculation orchestration instance
        var inputParameter = new NotifyAggregatedMeasureDataInputV1(
            CalculationTypes.WholesaleFixing,
            GridAreaCodes: new[] { "543" },
            PeriodStartDate: DateTimeOffset.Parse("2024-10-08T15:19:10.0151351+01:00"),
            PeriodEndDate: DateTimeOffset.Parse("2024-10-11T16:19:10.0193962+01:00"),
            IsInternalCalculation: false);
        var orchestrationInstanceId = await processManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartCalculationCommandV1(
                    userIdentity,
                    inputParameter),
                CancellationToken.None);

        // Step 2: Query until terminated with succeeded
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await processManagerClient
                    .GetOrchestrationInstanceByIdAsync<NotifyAggregatedMeasureDataInputV1>(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleStates.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationStates.Succeeded;
            },
            timeLimit: TimeSpan.FromSeconds(60),
            delay: TimeSpan.FromSeconds(3));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");

        // Step 3: General search using name and termination state
        var orchestrationInstancesGeneralSearch = await processManagerClient
            .SearchOrchestrationInstancesByNameAsync<NotifyAggregatedMeasureDataInputV1>(
                new SearchOrchestrationInstancesByNameQuery(
                    userIdentity,
                    name: new Brs_023_027_V1().Name,
                    version: null,
                    lifecycleState: OrchestrationInstanceLifecycleStates.Terminated,
                    terminationState: OrchestrationInstanceTerminationStates.Succeeded,
                    startedAtOrLater: null,
                    terminatedAtOrEarlier: null),
                CancellationToken.None);

        orchestrationInstancesGeneralSearch.Should().Contain(x => x.Id == orchestrationInstanceId);

        // TODO: Enable when custom filtering has been implemented correct
        ////// Step 4: Custom search
        ////var customQuery = new CalculationQuery(userIdentity)
        ////{
        ////    CalculationTypes = new[] { inputParameter.CalculationType },
        ////    GridAreaCodes = inputParameter.GridAreaCodes,
        ////    PeriodStartDate = inputParameter.PeriodStartDate,
        ////    PeriodEndDate = inputParameter.PeriodEndDate,
        ////    IsInternalCalculation = inputParameter.IsInternalCalculation,
        ////};
        ////var orchestrationInstancesCustomSearch = await processManagerClient
        ////    .SearchOrchestrationInstancesByNameAsync(
        ////        customQuery,
        ////        CancellationToken.None);

        ////orchestrationInstancesCustomSearch.Should().Contain(x => x.Id == orchestrationInstanceId);
        ////orchestrationInstancesCustomSearch.Count.Should().Be(1);
    }

    [Fact]
    public async Task Calculation_WhenScheduledToRunInThePast_CanMonitorLifecycle()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Step 1: Schedule new calculation orchestration instance
        var orchestrationInstanceId = await processManagerClient
            .ScheduleNewOrchestrationInstanceAsync(
                new ScheduleCalculationCommandV1(
                    userIdentity,
                    runAt: DateTimeOffset.Parse("2024-11-01T06:19:10.0209567+01:00"),
                    inputParameter: new NotifyAggregatedMeasureDataInputV1(
                        CalculationTypes.BalanceFixing,
                        GridAreaCodes: new[] { "543" },
                        PeriodStartDate: DateTimeOffset.Parse("2024-10-29T15:19:10.0151351+01:00"),
                        PeriodEndDate: DateTimeOffset.Parse("2024-10-29T16:19:10.0193962+01:00"),
                        IsInternalCalculation: true)),
                CancellationToken.None);

        // Step 2: Trigger the scheduler to queue the calculation orchestration instance
        await ProcessManagerAppFixture.AppHostManager
            .TriggerFunctionAsync("StartScheduledOrchestrationInstances");

        // Step 3: Query until terminated with succeeded
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await processManagerClient
                    .GetOrchestrationInstanceByIdAsync<NotifyAggregatedMeasureDataInputV1>(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleStates.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationStates.Succeeded;
            },
            timeLimit: TimeSpan.FromSeconds(60),
            delay: TimeSpan.FromSeconds(3));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }

    [Fact]
    public async Task CalculationScheduledToRunInTheFuture_WhenCanceled_CanMonitorLifecycle()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        // Step 1: Schedule new calculation orchestration instance
        var orchestrationInstanceId = await processManagerClient
            .ScheduleNewOrchestrationInstanceAsync(
                new ScheduleCalculationCommandV1(
                    userIdentity,
                    runAt: DateTimeOffset.Parse("2050-01-01T12:00:00.0000000+01:00"),
                    inputParameter: new NotifyAggregatedMeasureDataInputV1(
                        CalculationTypes.BalanceFixing,
                        GridAreaCodes: new[] { "543" },
                        PeriodStartDate: DateTimeOffset.Parse("2024-10-29T15:19:10.0151351+01:00"),
                        PeriodEndDate: DateTimeOffset.Parse("2024-10-29T16:19:10.0193962+01:00"),
                        IsInternalCalculation: true)),
                CancellationToken.None);

        // Step 2: Cancel the calculation orchestration instance
        await processManagerClient
            .CancelScheduledOrchestrationInstanceAsync(
                new CancelScheduledOrchestrationInstanceCommand(
                    userIdentity,
                    orchestrationInstanceId),
                CancellationToken.None);

        // Step 3: Query until terminated with user canceled
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await processManagerClient
                    .GetOrchestrationInstanceByIdAsync<NotifyAggregatedMeasureDataInputV1>(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleStates.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationStates.UserCanceled;
            },
            timeLimit: TimeSpan.FromSeconds(60),
            delay: TimeSpan.FromSeconds(3));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }

    private IConfiguration CreateInMemoryConfigurations(Dictionary<string, string?> configurations)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(configurations)
            .Build();
    }
}