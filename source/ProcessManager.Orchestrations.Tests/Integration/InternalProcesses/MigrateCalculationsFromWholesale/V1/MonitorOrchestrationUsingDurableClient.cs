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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.InternalProcesses.MigrateCalculationsFromWholesale.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Models;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Shared.Tests.Models;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.InternalProcesses.MigrateCalculationsFromWholesale.V1;

[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingDurableClient : IAsyncLifetime
{
    public MonitorOrchestrationUsingDurableClient(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                = AuthenticationOptionsForTests.ApplicationIdUri,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = Fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
        });
        services.AddProcessManagerHttpClients();
        ServiceProvider = services.BuildServiceProvider();

        ProcessManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
    }

    private OrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    private IProcessManagerClient ProcessManagerClient { get; }

    public async Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Assert that we can run a full orchestration:
    /// - The expected wholesale calculation id's are returned from the GetCalculationsToMigrateActivity.
    /// - A MigrateCalculationActivity is called for each wholesale calculation id.
    /// - The orchestration instance should complete with success.
    /// </summary>
    [Fact]
    public async Task Given_WholesaleCalculationsToMigrate_When_OrchestrationIsCompleted_Then_HasExpectedHistory()
    {
        // Given
        // => Wholesale calculations to migrate
        List<Guid> expectedWholesaleCalculationIds;
        await using (var wholesaleContext = Fixture.OrchestrationsAppManager.WholesaleDatabaseManager.CreateDbContext())
        {
            expectedWholesaleCalculationIds = await wholesaleContext.Calculations
                .Where(c => c.OrchestrationState == CalculationOrchestrationState.Completed)
                .Select(c => c.Id)
                .ToListAsync();
        }

        // => Clean database for orchestration instances to not get already migrated calculations
        await using (var processManagerContext = Fixture.OrchestrationsAppManager.DatabaseManager.CreateDbContext())
        {
            await processManagerContext.Database.ExecuteSqlAsync($"DELETE FROM [pm].[StepInstance]");
            await processManagerContext.Database.ExecuteSqlAsync($"DELETE FROM [pm].[OrchestrationInstance]");
        }

        // When
        // => Start new orchestration instance
        var userIdentityDto = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorNumber: ActorNumber.Create("0000000000000"),
            ActorRole: ActorRole.DataHubAdministrator,
            UserPermissions: []);

        var orchestrationInstanceId = await ProcessManagerClient.StartNewOrchestrationInstanceAsync(
            new MigrateCalculationsFromWholesaleCommandV1(
                userIdentityDto),
            CancellationToken.None);

        // => Wait for orchestration to be completed
        var completeOrchestrationStatus = await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationInstanceId.ToString(),
            TimeSpan.FromSeconds(60));

        // Then
        // => Verify the history of the orchestration
        var activities = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList();

        var expectedMigrateCalculationActivities = expectedWholesaleCalculationIds
            .Select(id => new OrchestrationHistoryItem(
                EventType: "TaskCompleted",
                FunctionName: nameof(MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1)))
            .ToList();

        List<OrchestrationHistoryItem> expectedActivities =
        [
            new("ExecutionStarted", FunctionName: nameof(Orchestration_MigrateCalculationsFromWholesale_V1)),
            new("TaskCompleted", FunctionName: nameof(TransitionOrchestrationToRunningActivity_V1)),

            new("TaskCompleted", FunctionName: nameof(TransitionStepToRunningActivity_V1)),
            new("TaskCompleted", FunctionName: nameof(GetCalculationsToMigrateActivity_MigrateCalculationsFromWholesale_V1)),
            new("TaskCompleted", FunctionName: nameof(TransitionStepToTerminatedActivity_V1)),

            new("TaskCompleted", FunctionName: nameof(TransitionStepToRunningActivity_V1)),
            ..expectedMigrateCalculationActivities,
            new("TaskCompleted", FunctionName: nameof(ValidateMigratedCalculationsActivity_MigrateCalculationsFromWholesale_V1)),
            new("TaskCompleted", FunctionName: nameof(TransitionStepToTerminatedActivity_V1)),

            new("TaskCompleted", FunctionName: nameof(TransitionOrchestrationToTerminatedActivity_V1)),
            new("ExecutionCompleted"),
        ];

        using var assertionScope = new AssertionScope { FormattingOptions = { MaxLines = 2000, } };
        completeOrchestrationStatus.CustomStatus.Should().NotBeNull();
        completeOrchestrationStatus.CustomStatus.Value<int>(nameof(CalculationsToMigrate.AllWholesaleCalculationsCount))
            .Should().Be(expectedWholesaleCalculationIds.Count);
        completeOrchestrationStatus.CustomStatus.Value<int>(nameof(CalculationsToMigrate.CalculationsToMigrateCount))
            .Should().Be(expectedWholesaleCalculationIds.Count);
        completeOrchestrationStatus.CustomStatus.Value<int>(nameof(CalculationsToMigrate.AlreadyMigratedCalculationsCount))
            .Should().Be(0);

        activities.Should().NotBeNull().And.Equal(expectedActivities);

        // => Verify that the durable function completed successfully
        completeOrchestrationStatus.RuntimeStatus.Should().Be(OrchestrationRuntimeStatus.Completed);
    }
}
