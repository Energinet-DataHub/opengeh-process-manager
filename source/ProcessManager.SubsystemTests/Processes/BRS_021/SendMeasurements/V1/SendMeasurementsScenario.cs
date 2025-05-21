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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.SendMeasurements.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_026_028.BRS_026.V1;
using NodaTime;
using NodaTime.Text;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_021.SendMeasurements.V1;

[TestCaseOrderer(
    ordererTypeName: TestCaseOrdererLocation.OrdererTypeName,
    ordererAssemblyName: TestCaseOrdererLocation.OrdererAssemblyName)]
public class SendMeasurementsScenario
    : IClassFixture<ProcessManagerFixture<SendMeasurementsScenarioState>>,
        IAsyncLifetime
{
    private readonly ProcessManagerFixture<SendMeasurementsScenarioState> _fixture;

    public SendMeasurementsScenario(
        ProcessManagerFixture<SendMeasurementsScenarioState> fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _fixture.SetTestOutputHelper(null);
        return Task.CompletedTask;
    }

    [SubsystemFact]
    [ScenarioStep(1)]
    public void Given_ValidSendMeasurementsCommand()
    {
        var start = Instant.FromUtc(2025, 05, 21, 23, 00, 00);
        var end = start.Plus(Duration.FromDays(1));
        var periodDuration = end - start;
        var resolution = Resolution.QuarterHourly;
        var numberOfMeasurements = (int)(periodDuration.TotalMinutes / 15); // Resolution = QuarterHourly = 15 minutes

        var actorMessageId = Guid.NewGuid().ToTestMessageUuid();
        var transactionId = Guid.NewGuid().ToTestMessageUuid();
        _fixture.TestConfiguration = new SendMeasurementsScenarioState(
            Command: new SendMeasurementsCommandV1(
                operatingIdentity: _fixture.GridAccessProviderActorIdentity,
                inputParameter: new SendMeasurementsInputV1(
                    ActorMessageId: actorMessageId,
                    TransactionId: transactionId,
                    ActorNumber: _fixture.GridAccessProviderActorIdentity.ActorNumber.Value,
                    ActorRole: _fixture.GridAccessProviderActorIdentity.ActorRole.Name,
                    BusinessReason: BusinessReason.PeriodicMetering.Name,
                    MeteringPointId: "123456789012345678",
                    MeteringPointType: MeteringPointType.Consumption.Name,
                    ProductNumber: null,
                    MeasureUnit: MeasurementUnit.KilowattHour.Name,
                    RegistrationDateTime: InstantPattern.General.Format(end),
                    Resolution: resolution.Name,
                    StartDateTime: InstantPattern.General.Format(start),
                    EndDateTime: InstantPattern.General.Format(end),
                    GridAccessProviderNumber: _fixture.GridAccessProviderActorIdentity.ActorNumber.Value,
                    MeteredDataList: Enumerable.Range(0, numberOfMeasurements)
                        .Select(
                            i => new SendMeasurementsInputV1.MeteredData(
                                Position: (i + 1).ToString(), // Position is 1 based
                                EnergyQuantity: "42.0",
                                QuantityQuality: Quality.AsProvided.Name))
                        .ToList()),
                idempotencyKey: transactionId));
    }

    [SubsystemFact]
    [ScenarioStep(2)]
    public async Task AndGiven_StartNewOrchestrationInstanceIsSent()
    {
        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            _fixture.TestConfiguration.Command,
            CancellationToken.None);
    }

    [SubsystemFact]
    [ScenarioStep(3)]
    public async Task When_OrchestrationInstanceIsRunning()
    {
        var (success, orchestrationInstance, _) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                SendMeasurementsInputV1, SendMeasurementsScenarioState>(
                _fixture.TestConfiguration.Command.IdempotencyKey,
                OrchestrationInstanceLifecycleState.Running);

        Assert.Multiple(
            () => Assert.True(
                success,
                $"An orchestration instance for idempotency key \"{_fixture.TestConfiguration.Command.IdempotencyKey}\" should have been found"),
            () => Assert.NotNull(orchestrationInstance));

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;
    }

    [SubsystemFact]
    [ScenarioStep(4)]
    public async Task Then_BusinessValidationIsSuccessful()
    {
        Assert.NotNull(_fixture.TestConfiguration.OrchestrationInstance); // If orchestration instance wasn't found in earlier test, end test early.

        var (success, orchestrationInstance, businessValidationStep) =
            await _fixture.WaitForOrchestrationInstanceByIdempotencyKeyAsync<
                SendMeasurementsInputV1, SendMeasurementsScenarioState>(
                idempotencyKey: _fixture.TestConfiguration.Command.IdempotencyKey,
                stepSequence: Orchestrations.Processes.BRS_021.SendMeasurements.V1.OrchestrationDescriptionBuilder.BusinessValidationStep,
                stepState: StepInstanceLifecycleState.Terminated);

        _fixture.TestConfiguration.OrchestrationInstance = orchestrationInstance;

        if (businessValidationStep?.CustomState.Length > 0)
            _fixture.Logger.WriteLine($"Business validation step custom state: {businessValidationStep?.CustomState}.");

        Assert.Multiple(
            () => Assert.True(success, $"Business validation step should be terminated."),
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, businessValidationStep?.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, businessValidationStep?.Lifecycle.TerminationState));
    }
}
