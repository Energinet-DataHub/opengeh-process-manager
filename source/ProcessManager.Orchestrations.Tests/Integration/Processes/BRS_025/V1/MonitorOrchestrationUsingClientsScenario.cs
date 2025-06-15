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

using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_025;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_025.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;
using Xunit.Abstractions;

using ElectricityMarketModels = Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_025.V1;

[ParallelWorkflow(WorkflowBucket.Bucket03)]
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private const string EnergySupplier = "1111111111111";
    private const string GridAccessProvider = "2222222222222";
    private const string NeighborGridAreaOwner1 = "3333333333331";
    private const string NeighborGridAreaOwner2 = "3333333333332";
    private const string GridArea = "804";
    private static readonly Instant _validFrom = Instant.FromUtc(2025, 11, 30, 23, 00, 00);
    private static readonly Instant _validTo = Instant.FromUtc(2025, 12, 31, 23, 00, 00);

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
        Fixture.EnqueueBrs025ServiceBusListener.ResetMessageHandlersAndReceivedMessages();
        Fixture.OrchestrationsAppManager.MockServer.Reset();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);
        Fixture.EnqueueBrs025ServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_ValidRequestMeasurements_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        // Setting up mock
        var now = SystemClock.Instance.GetCurrentInstant();
        var meteringPointId = "123456789012345678";
        Fixture.OrchestrationsAppManager.MockServer.MockGetAuthorizedPeriodsAsync(
            meteringPointId: meteringPointId,
            numberOfPeriods: 1);

        // TODO: This should not be "ByYear"
        Fixture.OrchestrationsAppManager.MockServer.MockGetAggregatedByYearForPeriodHttpResponse(
            meteringPointId: meteringPointId,
            from: now.PlusDays(-365),
            to: now)
;
        SetupElectricityMarketWireMocking(meteringPointId: meteringPointId);

        // Step 1: Start new orchestration instance
        var requestCommand = GivenCommand(meteringPointId: meteringPointId);

        await Fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            requestCommand,
            CancellationToken.None);

        // Step 2a: Query until waiting for EnqueueActorMessagesCompleted notify event
        var (isWaitingForNotify, orchestrationInstance) = await Fixture.ProcessManagerClient
            .WaitForStepToBeRunning<RequestMeasurementsInputV1>(
                requestCommand.IdempotencyKey,
                EnqueueActorMessagesStep.StepSequence);

        isWaitingForNotify.Should()
            .BeTrue("because the orchestration instance should wait for a EnqueueActorMessagesCompleted notify event");

        // Step 2b: Verify an enqueue actor messages event is sent on the service bus
        var verifyEnqueueActorMessagesEvent = await Fixture.EnqueueBrs025ServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_025.Name, out var enqueueActorMessagesV1))
                        return false;

                    var requestAcceptedV1 = enqueueActorMessagesV1.ParseData<RequestMeasurementsAcceptedV1>();

                    return requestAcceptedV1.OriginalTransactionId == requestCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue($"because a {nameof(RequestMeasurementsAcceptedV1)} service bus message should have been sent");

        // Step 3: Send EnqueueActorMessagesCompleted event
        await Fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new RequestMeasurementsNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstance!.Id.ToString()),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (orchestrationTerminated, terminatedOrchestrationInstance) = await Fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<RequestMeasurementsInputV1>(
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

    private RequestMeasurementsCommandV1 GivenCommand(
        string meteringPointId = "123456789012345678")
    {
        var input = new RequestMeasurementsInputV1(
            ActorMessageId: Guid.NewGuid().ToString(),
            TransactionId: Guid.NewGuid().ToString(),
            ActorNumber: EnergySupplier,
            ActorRole: ActorRole.EnergySupplier.Name,
            StartDateTime: "2025-01-07T22:00:00Z",
            EndDateTime: "2025-04-07T22:00:00Z",
            MeteringPointId: meteringPointId);

        return new RequestMeasurementsCommandV1(
            OperatingIdentity: Fixture.DefaultActorIdentity,
            InputParameter: input,
            IdempotencyKey: Guid.NewGuid().ToString());
    }

    private void SetupElectricityMarketWireMocking(
        ElectricityMarket.Integration.Models.MasterData.MeteringPointType? meteringPointType = null,
        string meteringPointId = "123456789012345678")
    {
        var meteringPointMasterData = new ElectricityMarket.Integration.Models.MasterData.MeteringPointMasterData
        {
            Identification = new ElectricityMarketModels.MeteringPointIdentification(meteringPointId),
            ValidFrom = _validFrom,
            ValidTo = _validTo,
            GridAreaCode = new ElectricityMarket.Integration.Models.MasterData.GridAreaCode(GridArea),
            GridAccessProvider = GridAccessProvider,
            NeighborGridAreaOwners = [NeighborGridAreaOwner1, NeighborGridAreaOwner2],
            ConnectionState = ElectricityMarket.Integration.Models.MasterData.ConnectionState.Connected,
            Type = meteringPointType ?? ElectricityMarket.Integration.Models.MasterData.MeteringPointType.Production,
            SubType = ElectricityMarket.Integration.Models.MasterData.MeteringPointSubType.Physical,
            Resolution = new ElectricityMarket.Integration.Models.MasterData.Resolution("PT1H"),
            Unit = ElectricityMarketModels.MeasureUnit.kWh,
            ProductId = ElectricityMarketModels.ProductId.Tariff,
            ParentIdentification = null,
            EnergySupplier = EnergySupplier,
        };

        Fixture.OrchestrationsAppManager.MockServer.MockElectricityMarketViewsMasterData(mockData: [
            meteringPointMasterData
        ]);
    }
}
