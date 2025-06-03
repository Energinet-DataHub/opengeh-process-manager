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
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;
using NodaTime;
using Xunit.Abstractions;

using ElectricityMarketModels = Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_024.V1;

[ParallelWorkflow(WorkflowBucket.Bucket03)]
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private const string MeteringPointId = "571313101700011887";
    private const string EnergySupplier = "1111111111111";
    private const string GridAccessProvider = "2222222222222";
    private const string NeighborGridAreaOwner1 = "3333333333331";
    private const string NeighborGridAreaOwner2 = "3333333333332";
    private const string GridArea = "804";
    private static readonly Instant _validFrom = Instant.FromUtc(2024, 11, 30, 23, 00, 00);
    private static readonly Instant _validTo = Instant.FromUtc(2024, 12, 31, 23, 00, 00);

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
        Fixture.EnqueueBrs024ServiceBusListener.ResetMessageHandlersAndReceivedMessages();
        Fixture.OrchestrationsAppManager.MockServer.Reset();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);
        Fixture.EnqueueBrs024ServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_ValidRequestYearlyMeasurements_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        // Setting up mock
        var now = SystemClock.Instance.GetCurrentInstant();
        var meteringPointId = "123456789012345678";
        Fixture.OrchestrationsAppManager.MockServer.MockGetAggregatedByYearForPeriodHttpResponse(
            meteringPointId: meteringPointId,
            from: now.PlusDays(-365),
            to: now)
;
        SetupElectricityMarketWireMocking();

        // Step 1: Start new orchestration instance
        var requestCommand = GivenCommand();

        await Fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            requestCommand,
            CancellationToken.None);

        // Step 2a: Query until waiting for EnqueueActorMessagesCompleted notify event
        var (isWaitingForNotify, orchestrationInstance) = await Fixture.ProcessManagerClient
            .WaitForStepToBeRunning<RequestYearlyMeasurementsInputV1>(
                requestCommand.IdempotencyKey,
                EnqueueActorMessagesStep.StepSequence);

        isWaitingForNotify.Should()
            .BeTrue("because the orchestration instance should wait for a EnqueueActorMessagesCompleted notify event");

        // Step 2b: Verify an enqueue actor messages event is sent on the service bus
        var verifyEnqueueActorMessagesEvent = await Fixture.EnqueueBrs024ServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_024.Name, out var enqueueActorMessagesV1))
                        return false;

                    var requestAcceptedV1 = enqueueActorMessagesV1.ParseData<RequestYearlyMeasurementsAcceptedV1>();

                    return requestAcceptedV1.OriginalTransactionId == requestCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue($"because a {nameof(RequestYearlyMeasurementsAcceptedV1)} service bus message should have been sent");

        // Step 3: Send EnqueueActorMessagesCompleted event
        await Fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new RequestYearlyMeasurementsNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstance!.Id.ToString()),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (orchestrationTerminated, terminatedOrchestrationInstance) = await Fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<RequestYearlyMeasurementsInputV1>(
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

    [Fact]
    public async Task Given_InvalidRequestYearlyMeasurements_When_Started_Then_OrchestrationInstanceTerminatesWithSuccess()
    {
        var invalidMeteringPointType = ElectricityMarket.Integration.Models.MasterData.MeteringPointType.Exchange;
        SetupElectricityMarketWireMocking(meteringPointType: invalidMeteringPointType);

        // Step 1: Start new orchestration instance
        var requestCommand = GivenCommand();

        await Fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            requestCommand,
            CancellationToken.None);

        // Step 2a: Query until waiting for EnqueueActorMessagesCompleted notify event
        var (isWaitingForNotify, orchestrationInstance) = await Fixture.ProcessManagerClient
            .WaitForStepToBeRunning<RequestYearlyMeasurementsInputV1>(
                requestCommand.IdempotencyKey,
                EnqueueActorMessagesStep.StepSequence);

        isWaitingForNotify.Should()
            .BeTrue("because the orchestration instance should wait for a EnqueueActorMessagesCompleted notify event");

        // Step 2b: Verify an enqueue actor messages event is sent on the service bus
        var verifyEnqueueActorMessagesEvent = await Fixture.EnqueueBrs024ServiceBusListener.When(
                (message) =>
                {
                    if (!message.TryParseAsEnqueueActorMessages(Brs_024.Name, out var enqueueActorMessagesV1))
                        return false;

                    var requestRejectV1 = enqueueActorMessagesV1.ParseData<RequestYearlyMeasurementsRejectV1>();

                    requestRejectV1.ValidationErrors.Should()
                        .HaveCount(1)
                        .And.Contain(
                            e => e.Message.Equals(
                                MeteringPointTypeValidationRule.WrongMeteringPointTypeError[0].Message));
                    return requestRejectV1.OriginalTransactionId == requestCommand.InputParameter.TransactionId;
                })
            .VerifyCountAsync(1);

        var enqueueMessageFound = verifyEnqueueActorMessagesEvent.Wait(TimeSpan.FromSeconds(30));
        enqueueMessageFound.Should().BeTrue($"because a {nameof(RequestYearlyMeasurementsRejectV1)} service bus message should have been sent");

        // Step 3: Send EnqueueActorMessagesCompleted event
        await Fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new RequestYearlyMeasurementsNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstance!.Id.ToString()),
            CancellationToken.None);

        // Step 4: Query until terminated
        var (orchestrationTerminated, terminatedOrchestrationInstance) = await Fixture.ProcessManagerClient
            .WaitForOrchestrationInstanceTerminated<RequestYearlyMeasurementsInputV1>(
                requestCommand.IdempotencyKey);

        orchestrationTerminated.Should().BeTrue(
            "because the orchestration instance should be terminated within the given wait time");

        // Orchestration instance and all steps should be Succeeded
        using var assertionScope = new AssertionScope();
        terminatedOrchestrationInstance!.Lifecycle.TerminationState.Should()
            .NotBeNull()
            .And.Be(OrchestrationInstanceTerminationState.Succeeded);

        terminatedOrchestrationInstance.Steps.OrderBy(s => s.Sequence)
            .Should()
            .SatisfyRespectively(
                s =>
                {
                    // Validation step should be failed
                    s.Sequence.Should().Be(BusinessValidationStep.StepSequence);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Failed);
                },
                s =>
                {
                    // Enqueue actor messages step should be succeeded
                    s.Sequence.Should().Be(EnqueueActorMessagesStep.StepSequence);
                    s.Lifecycle.State.Should().Be(StepInstanceLifecycleState.Terminated);
                    s.Lifecycle.TerminationState.Should()
                        .NotBeNull()
                        .And.Be(StepInstanceTerminationState.Succeeded);
                });
    }

    private RequestYearlyMeasurementsCommandV1 GivenCommand(
        string meteringPointId = "123456789012345678")
    {
        const string energySupplierNumber = "1234567891234";
        var energySupplierRole = ActorRole.EnergySupplier.Name;

        var input = new RequestYearlyMeasurementsInputV1(
            ActorMessageId: Guid.NewGuid().ToString(),
            TransactionId: Guid.NewGuid().ToString(),
            ActorNumber: energySupplierNumber,
            ActorRole: energySupplierRole,
            BusinessReason: BusinessReason.PeriodicMetering.Name,
            ReceivedAt: "2024-04-07T22:00:00Z",
            MeteringPointId: meteringPointId);

        return new RequestYearlyMeasurementsCommandV1(
            OperatingIdentity: Fixture.DefaultActorIdentity,
            InputParameter: input,
            IdempotencyKey: Guid.NewGuid().ToString());
    }

    private void SetupElectricityMarketWireMocking(
        ElectricityMarket.Integration.Models.MasterData.MeteringPointType? meteringPointType = null)
    {
        var meteringPointMasterData = new ElectricityMarket.Integration.Models.MasterData.MeteringPointMasterData
        {
            Identification = new ElectricityMarketModels.MeteringPointIdentification(MeteringPointId),
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
