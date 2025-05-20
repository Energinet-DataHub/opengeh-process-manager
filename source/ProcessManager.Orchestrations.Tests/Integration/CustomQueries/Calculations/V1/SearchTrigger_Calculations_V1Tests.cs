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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Databricks.Client.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.CustomQueries.Calculations.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to
/// perform a custom search for Calculations orchestration instances.
/// </summary>
[ParallelWorkflow(WorkflowBucket.Bucket01)]
[Collection(nameof(OrchestrationsAppCollection))]
public class SearchTrigger_Calculations_V1Tests : IAsyncLifetime
{
    public SearchTrigger_Calculations_V1Tests(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            // Process Manager HTTP client
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                = SubsystemAuthenticationOptionsForTests.ApplicationIdUri,
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

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        Fixture.OrchestrationsAppManager.EnsureAppHostUsesMockedDatabricksApi(true);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// This test proves that we can get type strong result objects return from the custom query.
    /// </summary>
    [Fact]
    public async Task CalculationsOrchestrationInstancesInDatabase_WhenQueryCalculations_ExpectedResultTypesAreRetrieved()
    {
        // Arrange

        // => Brs 023/027
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            "CalculatorJob");
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_023_027.V1.Model.StartCalculationCommandV1(
                    Fixture.DefaultUserIdentity,
                    new Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1(
                        Abstractions.Processes.BRS_023_027.V1.Model.CalculationType.WholesaleFixing,
                        GridAreaCodes: ["999"],
                        PeriodStartDate: new DateTimeOffset(2023, 1, 31, 23, 0, 0, TimeSpan.Zero),
                        PeriodEndDate: new DateTimeOffset(2023, 2, 28, 23, 0, 0, TimeSpan.Zero),
                        IsInternalCalculation: false)),
                CancellationToken.None);

        // => Brs 021 Electrical Heating
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            "ElectricalHeating");
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model.StartElectricalHeatingCalculationCommandV1(
                    Fixture.DefaultUserIdentity),
                CancellationToken.None);

        // => Brs 021 Capacity Settlement
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            "CapacitySettlement");
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_021.CapacitySettlementCalculation.V1.Model.StartCapacitySettlementCalculationCommandV1(
                    Fixture.DefaultUserIdentity,
                    new Abstractions.Processes.BRS_021.CapacitySettlementCalculation.V1.Model.CalculationInputV1(
                        Year: 2020,
                        Month: 1)),
                CancellationToken.None);

        // => Brs 021 Net Consumption
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            "NetConsumptionGroup6");
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_021.NetConsumptionCalculation.V1.Model.StartNetConsumptionCalculationCommandV1(
                    Fixture.DefaultUserIdentity),
                CancellationToken.None);

        // => Custom query
        var customQuery = new CalculationsQueryV1(Fixture.DefaultUserIdentity)
        {
            LifecycleStates = [
                OrchestrationInstanceLifecycleState.Queued,
                OrchestrationInstanceLifecycleState.Running,
                OrchestrationInstanceLifecycleState.Terminated],
        };

        // Act
        var actual = await ProcessManagerClient
            .SearchOrchestrationInstancesByCustomQueryAsync(
                customQuery,
                CancellationToken.None);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should()
            .Contain(x =>
                x.GetType() == typeof(WholesaleCalculationResultV1))
            .And.Contain(x =>
                x.GetType() == typeof(ElectricalHeatingCalculationResultV1))
            .And.Contain(x =>
                x.GetType() == typeof(CapacitySettlementCalculationResultV1))
            .And.Contain(x =>
                x.GetType() == typeof(NetConsumptionCalculationResultV1));
    }
}
