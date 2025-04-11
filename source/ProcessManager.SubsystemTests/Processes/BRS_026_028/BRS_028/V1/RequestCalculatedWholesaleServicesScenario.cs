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

using Energinet.DataHub.Core.TestCommon.Xunit.Attributes;
using Energinet.DataHub.Core.TestCommon.Xunit.Orderers;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
using NodaTime;
using NodaTime.Text;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_026_028.BRS_028.V1;

[TestCaseOrderer(
    ordererTypeName: TestCaseOrdererLocation.OrdererTypeName,
    ordererAssemblyName: TestCaseOrdererLocation.OrdererAssemblyName)]
public class RequestCalculatedWholesaleServicesScenario : IClassFixture<ProcessManagerFixture<RequestCalculatedWholesaleServicesScenarioState>>
{
    private readonly ProcessManagerFixture<RequestCalculatedWholesaleServicesScenarioState> _fixture;

    public RequestCalculatedWholesaleServicesScenario(
        ProcessManagerFixture<RequestCalculatedWholesaleServicesScenarioState> fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);
    }

    [SubsystemFact]
    [ScenarioStep(1)]
    public void Given_ValidRequestCalculatedWholesaleServicesRequest()
    {
        var testUuid = Guid.NewGuid().ToTestMessageUuid();
        _fixture.TestConfiguration = new RequestCalculatedWholesaleServicesScenarioState(
            request: new RequestCalculatedWholesaleServicesCommandV1(
                operatingIdentity: _fixture.EnergySupplierActorIdentity,
                inputParameter: new RequestCalculatedWholesaleServicesInputV1(
                    ActorMessageId: testUuid,
                    TransactionId: Guid.NewGuid().ToString(),
                    RequestedForActorNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                    RequestedForActorRole: _fixture.EnergySupplierActorIdentity.ActorRole.Name,
                    RequestedByActorNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                    RequestedByActorRole: _fixture.EnergySupplierActorIdentity.ActorRole.Name,
                    BusinessReason: BusinessReason.WholesaleFixing.Name,
                    Resolution: null,
                    PeriodStart: InstantPattern.General.Format(Instant.FromUtc(2025, 01, 31, 23, 00)),
                    PeriodEnd: InstantPattern.General.Format(Instant.FromUtc(2025, 02, 28, 23, 00)),
                    EnergySupplierNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                    ChargeOwnerNumber: null,
                    GridAreas: ["804"],
                    SettlementVersion: null,
                    ChargeTypes: null),
                idempotencyKey: testUuid));
    }

    [SubsystemFact]
    [ScenarioStep(2)]
    public async Task AndGiven_StartNewOrchestrationInstanceIsSent()
    {
        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            _fixture.TestConfiguration.Request,
            CancellationToken.None);
    }

    [SubsystemFact]
    [ScenarioStep(3)]
    public async Task When_OrchestrationInstanceIsStarted()
    {
        var (success, orchestrationInstance, _) =
            await _fixture.WaitForOrchestrationInstanceAsync<RequestCalculatedWholesaleServicesInputV1>(
                _fixture.TestConfiguration.Request.IdempotencyKey);

        Assert.Multiple(
            () => Assert.True(
                success,
                $"An orchestration instance for idempotency key \"{_fixture.TestConfiguration.Request.IdempotencyKey}\" should have been found"),
            () => Assert.NotNull(orchestrationInstance));

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;
    }

    [SubsystemFact]
    [ScenarioStep(4)]
    public void Then_OrchestrationInstanceHasCorrectValues()
    {
        var request = _fixture.TestConfiguration.Request;
        var orchestrationInstance = _fixture.TestConfiguration.OrchestrationInstance;

        Assert.NotNull(orchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        Assert.Multiple(
            () => Assert.Equal(request.IdempotencyKey, orchestrationInstance.IdempotencyKey),
            () => Assert.Contains(orchestrationInstance.Lifecycle.State, new[] { OrchestrationInstanceLifecycleState.Queued, OrchestrationInstanceLifecycleState.Running, OrchestrationInstanceLifecycleState.Terminated }),
            () => Assert.Null(orchestrationInstance.Lifecycle.TerminationState),
            () => Assert.Equal(2, orchestrationInstance.Steps.Count),
            () => Assert.Equivalent(request.InputParameter, orchestrationInstance.ParameterValue),
            () => Assert.Equal(request.ActorMessageId, orchestrationInstance.ActorMessageId),
            () => Assert.Equal(request.TransactionId, orchestrationInstance.TransactionId),
            () => Assert.Null(orchestrationInstance.MeteringPointId));
    }

    [SubsystemFact]
    [ScenarioStep(5)]
    public async Task AndThen_BusinessValidationStepIsSuccessful()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, businessValidationStep) =
            await _fixture.WaitForOrchestrationInstanceAsync<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: _fixture.TestConfiguration.Request.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_026_028.BRS_028.V1.Orchestration.Steps.BusinessValidationStep.StepSequence,
                stepState: StepInstanceLifecycleState.Terminated);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        if (businessValidationStep?.CustomState.Length > 0)
            _fixture.Logger.WriteLine($"Business validation step custom state: {businessValidationStep?.CustomState}.");

        Assert.Multiple(
            () => Assert.True(success, $"Business validation step should be terminated."),
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, businessValidationStep?.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, businessValidationStep?.Lifecycle.TerminationState));
    }

    [SubsystemFact]
    [ScenarioStep(6)]
    public async Task AndThen_EnqueueActorMessagesStepIsRunning()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, enqueueActorMessagesStep) =
            await _fixture.WaitForOrchestrationInstanceAsync<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: _fixture.TestConfiguration.Request.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_026_028.BRS_028.V1.Orchestration.Steps.EnqueueActorMessagesStep.StepSequence,
                stepState: StepInstanceLifecycleState.Running);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Enqueue actor messages step should be running"),
            () => Assert.Equal(StepInstanceLifecycleState.Running, enqueueActorMessagesStep?.Lifecycle.State),
            () => Assert.Null(enqueueActorMessagesStep?.Lifecycle.TerminationState));
    }

    [SubsystemFact]
    [ScenarioStep(7)]
    public async Task AndThen_ReceivingNotifyEnqueueActorMessagesCompletedTransitionsEnqueueActorMessagesStepToSuccessful()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        // Send notify "EnqueueActorMessagesCompleted" message to the orchestration instance
        await _fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new RequestCalculatedWholesaleServicesNotifyEventV1(
                OrchestrationInstanceId: _fixture.TestConfiguration.OrchestrationInstance.Id.ToString()),
            CancellationToken.None);

        // Wait for the enqueue actor messages step to be terminated
        var (success, orchestrationInstance, enqueueActorMessagesStep) =
            await _fixture.WaitForOrchestrationInstanceAsync<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: _fixture.TestConfiguration.Request.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_026_028.BRS_028.V1.Orchestration.Steps.EnqueueActorMessagesStep.StepSequence,
                stepState: StepInstanceLifecycleState.Terminated);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Enqueue actor messages step should be terminated"),
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, enqueueActorMessagesStep?.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, enqueueActorMessagesStep?.Lifecycle.TerminationState));
    }

    [SubsystemFact]
    [ScenarioStep(8)]
    public async Task AndThen_OrchestrationInstanceIsTerminatedWithSuccess()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, _) =
            await _fixture.WaitForOrchestrationInstanceAsync<RequestCalculatedWholesaleServicesInputV1>(
                idempotencyKey: _fixture.TestConfiguration.Request.IdempotencyKey,
                orchestrationInstanceState: OrchestrationInstanceLifecycleState.Terminated);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "The orchestration instance should be terminated"),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, orchestrationInstance?.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, orchestrationInstance?.Lifecycle.TerminationState));
    }
}
