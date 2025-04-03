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

using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.Core.TestCommon.Xunit.Attributes;
using Energinet.DataHub.Core.TestCommon.Xunit.Orderers;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_026_028.BRS_026.V1;

[TestCaseOrderer(
    ordererTypeName: TestCaseOrdererLocation.OrdererTypeName,
    ordererAssemblyName: TestCaseOrdererLocation.OrdererAssemblyName)]
public class RequestCalculatedEnergyTimeSeriesTests : IClassFixture<ProcessManagerFixture<RequestCalculatedEnergyTimeSeriesTestConfiguration>>
{
    private readonly ProcessManagerFixture<RequestCalculatedEnergyTimeSeriesTestConfiguration> _fixture;

    public RequestCalculatedEnergyTimeSeriesTests(
        ProcessManagerFixture<RequestCalculatedEnergyTimeSeriesTestConfiguration> fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);
    }

    [Fact]
    [ScenarioStep(1)]
    public void Given_ValidRequestCalculatedEnergyTimeSeriesRequest()
    {
        _fixture.TestConfiguration = new RequestCalculatedEnergyTimeSeriesTestConfiguration(
            request: new RequestCalculatedEnergyTimeSeriesCommandV1(
                operatingIdentity: _fixture.EnergySupplierActorIdentity,
                inputParameter: new RequestCalculatedEnergyTimeSeriesInputV1(
                    ActorMessageId: Guid.NewGuid().ToTestUuid(),
                    TransactionId: Guid.NewGuid().ToString(),
                    RequestedForActorNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                    RequestedForActorRole: _fixture.EnergySupplierActorIdentity.ActorRole.Name,
                    RequestedByActorNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                    RequestedByActorRole: _fixture.EnergySupplierActorIdentity.ActorRole.Name,
                    BusinessReason: BusinessReason.BalanceFixing.Name,
                    PeriodStart: new DateTimeOffset(year: 2025, month: 03, day: 07, hour: 23, minute: 00, second: 00, offset: TimeSpan.Zero).ToString(),
                    PeriodEnd: new DateTimeOffset(year: 2025, month: 03, day: 09, hour: 23, minute: 00, second: 00, offset: TimeSpan.Zero).ToString(),
                    EnergySupplierNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                    BalanceResponsibleNumber: null,
                    GridAreas: ["804"],
                    MeteringPointType: null,
                    SettlementMethod: null,
                    SettlementVersion: null),
                idempotencyKey: Guid.NewGuid().ToString()));
    }

    [Fact]
    [ScenarioStep(2)]
    public async Task AndGiven_StartNewOrchestrationInstanceIsSent()
    {
        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            _fixture.TestConfiguration.Request,
            CancellationToken.None);
    }

    [Fact]
    [ScenarioStep(3)]
    public async Task When_OrchestrationInstanceIsStarted()
    {
        var idempotencyKey = _fixture.TestConfiguration.Request.IdempotencyKey;

        var (success, orchestrationInstance, _) = await WaitForOrchestrationInstance();

        Assert.Multiple(
            () => Assert.True(success, $"An orchestration instance for idempotency key \"{idempotencyKey}\" should have been found"),
            () => Assert.NotNull(orchestrationInstance));

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;
    }

    [Fact]
    [ScenarioStep(4)]
    public void Then_OrchestrationInstanceHasCorrectValues()
    {
        var request = _fixture.TestConfiguration.Request;
        var orchestrationInstance = _fixture.TestConfiguration.OrchestrationInstance;

        Assert.NotNull(orchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        Assert.Multiple(
            () => Assert.Equal(request.IdempotencyKey, orchestrationInstance.IdempotencyKey),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Running, orchestrationInstance.Lifecycle.State),
            () => Assert.Null(orchestrationInstance.Lifecycle.TerminationState),
            () => Assert.Equal(2, orchestrationInstance.Steps.Count),
            () => Assert.Equivalent(request.InputParameter, orchestrationInstance.ParameterValue),
            () => Assert.Equal(request.ActorMessageId, orchestrationInstance.ActorMessageId),
            () => Assert.Equal(request.TransactionId, orchestrationInstance.TransactionId),
            () => Assert.Null(orchestrationInstance.MeteringPointId));
    }

    [Fact]
    [ScenarioStep(5)]
    public async Task AndThen_BusinessValidationStepIsSuccessful()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, businessValidationStep) = await WaitForOrchestrationInstance(
            stepSequence: Orchestrations.Processes.BRS_026_028.BRS_026.V1.Orchestration.Steps.BusinessValidationStep.StepSequence,
            stepState: StepInstanceLifecycleState.Terminated);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Business validation step should be terminated"),
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, businessValidationStep?.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, businessValidationStep?.Lifecycle.TerminationState));
    }

    [Fact]
    [ScenarioStep(6)]
    public async Task AndThen_EnqueueActorMessagesStepIsRunning()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, enqueueActorMessagesStep) = await WaitForOrchestrationInstance(
            stepSequence: Orchestrations.Processes.BRS_026_028.BRS_026.V1.Orchestration.Steps.EnqueueActorMessagesStep.StepSequence,
            stepState: StepInstanceLifecycleState.Running);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Enqueue actor messages step should be running"),
            () => Assert.Equal(StepInstanceLifecycleState.Running, enqueueActorMessagesStep?.Lifecycle.State),
            () => Assert.Null(enqueueActorMessagesStep?.Lifecycle.TerminationState));
    }

    [Fact]
    [ScenarioStep(7)]
    public async Task AndThen_ReceivingNotifyEnqueueActorMessagesCompletedTransitionsEnqueueActorMessagesStepToSuccessful()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        // Send notify "EnqueueActorMessagesCompleted" message for the orchestration instance
        await _fixture.ProcessManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new RequestCalculatedEnergyTimeSeriesNotifyEventV1(
                OrchestrationInstanceId: _fixture.TestConfiguration.OrchestrationInstance.Id.ToString()),
            CancellationToken.None);

        var (success, orchestrationInstance, enqueueActorMessagesStep) = await WaitForOrchestrationInstance(
            stepSequence: Orchestrations.Processes.BRS_026_028.BRS_026.V1.Orchestration.Steps.EnqueueActorMessagesStep.StepSequence,
            stepState: StepInstanceLifecycleState.Terminated);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "Enqueue actor messages step should be terminated"),
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, enqueueActorMessagesStep?.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, enqueueActorMessagesStep?.Lifecycle.TerminationState));
    }

    [Fact]
    [ScenarioStep(8)]
    public async Task AndThen_OrchestrationInstanceIsTerminatedWithSuccess()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, _) = await WaitForOrchestrationInstance(
            orchestrationInstanceState: OrchestrationInstanceLifecycleState.Terminated);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        Assert.Multiple(
            () => Assert.True(success, "The orchestration instance should be terminated"),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, orchestrationInstance?.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, orchestrationInstance?.Lifecycle.TerminationState));
    }

    /// <summary>
    /// Wait for an orchestration instance to be returned by the ProcessManager http client. If step inputs are provided,
    /// then the orchestration instance must have a step instance with the given step sequence and state.
    /// <remarks>The lookup is based on the idempotency key of the test configuration request.</remarks>
    /// </summary>
    /// <param name="orchestrationInstanceState">If provided, then the orchestration instance have the given state.</param>
    /// <param name="stepSequence">If provided, then the orchestration instance must have a step instance with the given sequence number.</param>
    /// <param name="stepState">If provided, then the step should be in the given state (defaults to <see cref="StepInstanceLifecycleState.Terminated"/>).</param>
    private async Task<(
        bool Success,
        OrchestrationInstanceTypedDto<RequestCalculatedEnergyTimeSeriesInputV1>? OrchestrationInstance,
        StepInstanceDto? StepInstance)> WaitForOrchestrationInstance(
            OrchestrationInstanceLifecycleState? orchestrationInstanceState = null,
            int? stepSequence = null,
            StepInstanceLifecycleState? stepState = null)
    {
        if (stepState != null && stepSequence == null)
            throw new ArgumentNullException(nameof(stepSequence), $"{nameof(stepSequence)} must be provided if {nameof(stepState)} is not null.");

        OrchestrationInstanceTypedDto<RequestCalculatedEnergyTimeSeriesInputV1>? orchestrationInstance = null;
        StepInstanceDto? stepInstance = null;

        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                orchestrationInstance = await _fixture.ProcessManagerHttpClient
                    .GetOrchestrationInstanceByIdempotencyKeyAsync<RequestCalculatedEnergyTimeSeriesInputV1>(
                        new GetOrchestrationInstanceByIdempotencyKeyQuery(
                            operatingIdentity: _fixture.UserIdentity,
                            idempotencyKey: _fixture.TestConfiguration.Request.IdempotencyKey),
                        CancellationToken.None);

                if (orchestrationInstance == null)
                    return false;

                if (stepSequence != null)
                {
                    stepInstance = orchestrationInstance.Steps
                        .SingleOrDefault(s => s.Sequence == stepSequence.Value);
                }

                if (orchestrationInstanceState != null && orchestrationInstance.Lifecycle.State != orchestrationInstanceState)
                    return false;

                // If step sequence is not provided, only check for orchestration instance existence
                if (stepSequence == null)
                    return true;

                return stepInstance != null
                    ? stepInstance.Lifecycle.State == (stepState ?? StepInstanceLifecycleState.Terminated)
                    : throw new ArgumentException($"Step instance for step sequence {stepSequence} not found", nameof(stepSequence));
            },
            timeLimit: TimeSpan.FromMinutes(1),
            delay: TimeSpan.FromSeconds(1));

        return (success, orchestrationInstance, stepInstance);
    }
}
