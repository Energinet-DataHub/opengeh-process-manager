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
public class SearchTrigger_CalculationById_V1Tests : IAsyncLifetime
{
    public SearchTrigger_CalculationById_V1Tests(
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
    public async Task Given_ElectricalHeatingOrchestrationInstanceInDatabase_When_QueryById_Then_ReturnsExpectedResultType()
    {
        // Given
        // => Brs 021 Electrical Heating
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            "ElectricalHeating");
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        var orchestrationInstanceId = await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model.StartElectricalHeatingCalculationCommandV1(
                    Fixture.DefaultUserIdentity),
                CancellationToken.None);

        // => Custom query
        var customQuery = new CalculationByIdQueryV1(
            Fixture.DefaultUserIdentity,
            orchestrationInstanceId);

        // When
        var actual = await ProcessManagerClient
            .SearchOrchestrationInstanceByCustomQueryAsync(
                customQuery,
                CancellationToken.None);

        // Then
        using var assertionScope = new AssertionScope();
        actual.Should()
            .BeOfType<ElectricalHeatingCalculationResultV1>()
            .Which.Id.Should().Be(orchestrationInstanceId);
    }

    /// <summary>
    /// This test proves that we can get "null" return from the custom query, if the type doesn't match supported Calculations.
    /// </summary>
    public async Task Given_UnsupportedCalculationOrchestrationInstanceInDatabase_When_QueryById_Then_ReturnsNull()
    {
        // Given
        // => Brs 045 Missing Measurements Log
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            "MissingMeasurementsLogOnDemand");
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        var orchestrationInstanceId = await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model.
                    StartMissingMeasurementsLogOnDemandCalculationCommandV1(
                        Fixture.DefaultUserIdentity,
                        new Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model.
                            CalculationInputV1(
                                new DateTimeOffset(2025, 1, 31, 23, 0, 0, TimeSpan.Zero),
                                new DateTimeOffset(2025, 2, 28, 23, 0, 0, TimeSpan.Zero),
                                ["804"])),
                CancellationToken.None);

        // => Custom query
        var customQuery = new CalculationByIdQueryV1(
            Fixture.DefaultUserIdentity,
            orchestrationInstanceId);

        // When
        var actual = await ProcessManagerClient
            .SearchOrchestrationInstanceByCustomQueryAsync(
                customQuery,
                CancellationToken.None);

        // Then
        actual.Should().BeNull();
    }
}
