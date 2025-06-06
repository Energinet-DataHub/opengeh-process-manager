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

using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.Shared;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Databricks.Client.Models;
using NodaTime;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to start a
/// calculation orchestration (with input parameters) and monitor its status during its lifetime.
/// </summary>
[ParallelWorkflow(WorkflowBucket.Bucket05)]
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private const string CalculationJobName = "MissingMeasurementsLogOnDemand";

    public MonitorOrchestrationUsingClientsScenario(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private OrchestrationsAppFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        Fixture.OrchestrationsAppManager.EnsureAppHostUsesMockedDatabricksApi(true);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Calculation_WhenStarted_CanMonitorLifecycle()
    {
        // Mocking the databricks api. Forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        // Mocking EDI enqueue actor messages response
        Fixture.OrchestrationsAppManager.MockServer.MockEnqueueActorMessagesHttpResponse(
            EnqueueMissingMeasurementsLogHttpV1.RouteName);

        const string meteringPointId = "1000000000000001";

        // Mocking the Electricity Market Views master data API
        Fixture.OrchestrationsAppManager.MockServer.MockElectricityMarketViewsMasterData(mockData: [
            new MeteringPointMasterData
            {
                Identification = new MeteringPointIdentification(meteringPointId),
                ValidFrom = Instant.FromUtc(2025, 01, 24, 22, 00),
                ValidTo = Instant.FromUtc(2025, 12, 25, 22, 00),
                GridAreaCode = new GridAreaCode("301"),
                GridAccessProvider = "1111111111111",
                NeighborGridAreaOwners = ["2222222222222"],
                ConnectionState = ConnectionState.Connected,
                Type = MeteringPointType.ElectricalHeating, // TODO The correct metering point type is not defined yet.
                SubType = MeteringPointSubType.Physical,
                Resolution = new Resolution("PT15M"),
                Unit = MeasureUnit.kWh,
                ProductId = ProductId.EnergyActive,
                ParentIdentification = null,
                EnergySupplier = "3333333333333",
            },
        ]);

        // Step 1: Start new calculation orchestration instance
        var orchestrationInstanceId = await Fixture.ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartMissingMeasurementsLogOnDemandCalculationCommandV1(
                    Fixture.DefaultUserIdentity,
                    new CalculationInputV1(
                        new DateTimeOffset(2025, 1, 1, 23, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2025, 1, 10, 23, 0, 0, TimeSpan.Zero),
                        ["301"])),
                CancellationToken.None);

        // Mocking the databricks sql statements api
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksMissingMeasurementsLogQueryResponse(
            mockData:
            [
                new DatabricksSqlStatementApiMissingMeasurementsLogExtensions.MissingMeasurementsLogRowData(
                    OrchestrationInstanceId: orchestrationInstanceId,
                    MeteringPointId: meteringPointId,
                    Date: Instant.FromUtc(2025, 01, 29, 13, 30)),
            ]);

        // Step 2: Query until terminated
        var (isTerminated, terminatedOrchestrationInstance) = await Fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated(
                orchestrationInstanceId: orchestrationInstanceId);

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");

        // Then the orchestration instance (and its steps) should be terminated with success.
        using (_ = new AssertionScope())
        {
            // Orchestration instance and all steps should be Succeeded
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

        // And then enqueue actor messages is called for 1 message.
        Fixture.OrchestrationsAppManager.MockServer.CountEnqueueActorMessagesHttpMockCalls(
                routeName: EnqueueMissingMeasurementsLogHttpV1.RouteName)
            .Should()
            .Be(1, "because the orchestration instance should have enqueued messages to EDI");
    }
}
