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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.NetConsumptionCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.Shared.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Databricks.Client.Models;
using NodaTime;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.NetConsumptionCalculation.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to start a
/// calculation orchestration (with no input parameter) and monitor its status during its lifetime.
/// </summary>
[ParallelWorkflow(WorkflowBucket.Bucket04)]
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private const string CalculationJobName = "NetConsumptionGroup6";

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

        Fixture.OrchestrationsAppManager.MockServer.Reset();
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
    public async Task Given_StartNetConsumptionCalculationCommand_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        // Mocking the databricks jobs api, forcing it to return a terminated successful job status
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        // Mocking EDI enqueue actor messages response
        Fixture.OrchestrationsAppManager.MockServer.MockEnqueueActorMessagesHttpResponse(
            EnqueueCalculatedMeasurementsHttpV1.RouteName);

        const string meteringPointId = "1234567890123456";

        // Mocking the Electricity Market Views master data API
        Fixture.OrchestrationsAppManager.MockServer.MockElectricityMarketViewsMasterData(mockData: [
            new MeteringPointMasterData
            {
                Identification = new MeteringPointIdentification(meteringPointId),
                ValidFrom = Instant.FromUtc(2025, 04, 24, 22, 00),
                ValidTo = Instant.FromUtc(2025, 12, 25, 22, 00),
                GridAreaCode = new GridAreaCode("804"),
                GridAccessProvider = "1111111111111",
                NeighborGridAreaOwners = ["2222222222222"],
                ConnectionState = ConnectionState.Connected,
                Type = MeteringPointType.NetConsumption,
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
                new StartNetConsumptionCalculationCommandV1(
                    Fixture.DefaultUserIdentity),
                CancellationToken.None);

        // Mocking the databricks sql statements api
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksCalculatedMeasurementsQueryResponse(
            mockData:
            [
                new(
                    OrchestrationInstanceId: orchestrationInstanceId,
                    TransactionId: Guid.NewGuid(),
                    TransactionCreationDatetime: Instant.FromUtc(2025, 04, 25, 13, 37),
                    MeteringPointId: meteringPointId,
                    MeteringPointType: "net_consumption",
                    ObservationTime: Instant.FromUtc(2025, 04, 25, 13, 30),
                    Quantity: 1337.42m),
            ]);

        // Step 2: Query until terminated
        var (isTerminated, terminatedOrchestrationInstance) = await Fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated(
                orchestrationInstanceId: orchestrationInstanceId);

        isTerminated.Should().BeTrue("because the orchestration instance should be terminated within given wait time");

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
                routeName: EnqueueCalculatedMeasurementsHttpV1.RouteName)
            .Should()
            .Be(1, "because the orchestration instance should have enqueued messages to EDI");
    }
}
