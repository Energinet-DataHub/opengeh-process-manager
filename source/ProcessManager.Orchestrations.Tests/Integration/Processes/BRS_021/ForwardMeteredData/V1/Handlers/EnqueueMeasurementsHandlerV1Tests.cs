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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using StepInstanceTerminationState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.StepInstanceTerminationState;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class EnqueueMeasurementsHandlerV1Tests
    : IClassFixture<ProcessManagerDatabaseFixture>, IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IEnqueueActorMessagesClient> _enqueueActorMessagesClient = new();

    private readonly Instant _now = Instant.FromUtc(2025, 06, 06, 13, 37);
    private readonly MeteringPointId _meteringPointId = new("123456789012345678");
    private readonly string _actorMessageId = Guid.NewGuid().ToString();
    private readonly ActorNumber _gridAccessProvider = ActorNumber.Create("1111111111111");
    private readonly ActorNumber _energySupplier = ActorNumber.Create("1111111111112");

    public EnqueueMeasurementsHandlerV1Tests(ProcessManagerDatabaseFixture fixture)
    {
        _fixture = fixture;

        _clock.Setup(c => c.GetCurrentInstant()).Returns(_now);
    }

    [NotNull]
    private ProcessManagerContext? DbContext { get; set; }

    [NotNull]
    private OrchestrationDescription? OrchestrationDescription { get; set; }

    [NotNull]
    private EnqueueMeasurementsHandlerV1? Sut { get; set; }

    public async Task InitializeAsync()
    {
        DbContext = _fixture.DatabaseManager.CreateDbContext();

        OrchestrationDescription = await CreateSendMeasurementsOrchestrationDescriptionAsync();

        Sut = new EnqueueMeasurementsHandlerV1(
            new OrchestrationInstanceRepository(DbContext),
            _clock.Object,
            _enqueueActorMessagesClient.Object,
            new MeteringPointReceiversProvider(DateTimeZone.Utc),
            new TelemetryClient(new TelemetryConfiguration
            {
                TelemetryChannel = Mock.Of<ITelemetryChannel>(),
            }));

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (DbContext != null) // DbContext can be null if InitializeAsync fails
            await DbContext.DisposeAsync();
    }

    [Fact]
    public async Task Given_RunningOrchestrationInstance_When_HandleAsync_Then_ActorMessagesAreEnqueued()
    {
        // Arrange
        var orchestrationInstance = CreateRunningOrchestrationInstance();

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.OrchestrationInstances.Add(orchestrationInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(orchestrationInstance.Id);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleAsync(oi => oi.Id == orchestrationInstance.Id);

        var measurementsStep = actualOrchestrationInstance.GetStep(OrchestrationDescriptionBuilder.ForwardToMeasurementsStep);
        var enqueueStep = actualOrchestrationInstance.GetStep(OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);

        // - ForwardToMeasurements step should be terminated with success.
        // - EnqueueActorMessages step should be running.
        Assert.Multiple(
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, measurementsStep.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, measurementsStep.Lifecycle.TerminationState),
            () => Assert.Equal(_now, measurementsStep.Lifecycle.TerminatedAt),
            () => Assert.Equal(StepInstanceLifecycleState.Running, enqueueStep.Lifecycle.State),
            () => Assert.Equal(_now, enqueueStep.Lifecycle.StartedAt),
            () => Assert.Null(enqueueStep.Lifecycle.TerminationState));

        _enqueueActorMessagesClient.Verify(
            client => client.EnqueueAsync(
                Brs_021_ForwardedMeteredData.V1,
                orchestrationInstance.Id.Value,
                orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
                It.IsAny<Guid>(),
                It.Is<ForwardMeteredDataAcceptedV1>(m =>
                    m.MeteringPointId == _meteringPointId.Value &&
                    m.OriginalActorMessageId == _actorMessageId &&
                    HasCorrectReceivers(m))),
            Times.Once);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_RunningOrchestrationInstance_AndGiven_AlreadyHasEnqueueIdempotencyKey_When_HandleAsync_Then_ActorMessagesAreEnqueuedWithSameIdempotencyKey()
    {
        // Arrange
        var orchestrationInstance = CreateRunningOrchestrationInstance();

        var enqueueIdempotencyKey = Guid.NewGuid();
        var enqueueStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);
        enqueueStep.CustomState.SetFromInstance(
            new EnqueueActorMessagesStepCustomStateV1(
                IdempotencyKey: enqueueIdempotencyKey));

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.OrchestrationInstances.Add(orchestrationInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(orchestrationInstance.Id);

        // Assert
        _enqueueActorMessagesClient.Verify(
            client => client.EnqueueAsync(
                Brs_021_ForwardedMeteredData.V1,
                orchestrationInstance.Id.Value,
                orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
                enqueueIdempotencyKey,
                It.IsAny<ForwardMeteredDataAcceptedV1>()),
            Times.Once);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_OrchestrationInstanceStuckAtEnqueueActorMessages_When_HandleAsync_Then_ActorMessagesAreEnqueued()
    {
        // Arrange
        var orchestrationInstance = CreateRunningOrchestrationInstance();

        // Simulate that the orchestration instance is already has terminated the ForwardToMeasurementsStep and
        // is stuck at the EnqueueActorMessages step
        orchestrationInstance.TransitionStepToTerminated(
            sequence: OrchestrationDescriptionBuilder.ForwardToMeasurementsStep,
            StepInstanceTerminationState.Succeeded,
            _clock.Object);

        orchestrationInstance.TransitionStepToRunning(
            sequence: OrchestrationDescriptionBuilder.EnqueueActorMessagesStep,
            _clock.Object);

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.OrchestrationInstances.Add(orchestrationInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(orchestrationInstance.Id);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleAsync(oi => oi.Id == orchestrationInstance.Id);

        var enqueueStep = actualOrchestrationInstance.GetStep(OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);

        // - EnqueueActorMessages step should still be running.
        Assert.Multiple(
            () => Assert.Equal(StepInstanceLifecycleState.Running, enqueueStep.Lifecycle.State),
            () => Assert.Null(enqueueStep.Lifecycle.TerminationState),
            () => Assert.Null(enqueueStep.Lifecycle.TerminatedAt));

        // - Enqueue actor messages client should be called with the correct parameters.
        _enqueueActorMessagesClient.Verify(
            client => client.EnqueueAsync(
                Brs_021_ForwardedMeteredData.V1,
                orchestrationInstance.Id.Value,
                orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
                It.IsAny<Guid>(),
                It.Is<ForwardMeteredDataAcceptedV1>(m =>
                    m.MeteringPointId == _meteringPointId.Value &&
                    m.OriginalActorMessageId == _actorMessageId &&
                    HasCorrectReceivers(m))),
            Times.Once);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_TerminatedOrchestrationInstance_When_HandleAsync_Then_NothingHappens()
    {
        // Arrange
        var orchestrationInstance = CreateTerminatedOrchestrationInstance();

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.OrchestrationInstances.Add(orchestrationInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(orchestrationInstance.Id);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleAsync(oi => oi.Id == orchestrationInstance.Id);

        // - The orchestration instance should not be changed.
        Assert.Equivalent(orchestrationInstance, actualOrchestrationInstance);

        // - The enqueue actor messages client should not be called.
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_QueuedOrchestrationInstance_When_HandleAsync_Then_ThrowsException_AndThen_ActorMessagesNotEnqueued()
    {
        // Arrange
        var orchestrationInstance = CreateOrchestrationInstance();
        orchestrationInstance.Lifecycle.TransitionToQueued(_clock.Object);

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.OrchestrationInstances.Add(orchestrationInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        var act = () => Sut.HandleAsync(orchestrationInstance.Id);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);

        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleAsync(oi => oi.Id == orchestrationInstance.Id);

        // - The orchestration instance should not be changed.
        Assert.Equivalent(orchestrationInstance, actualOrchestrationInstance);

        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_ForwardToMeasurementsStepNotRunning_When_HandleAsync_Then_ThrowsException_AndThen_ActorMessagesNotEnqueued()
    {
        // Arrange
        var orchestrationInstance = CreateOrchestrationInstance();
        orchestrationInstance.Lifecycle.TransitionToQueued(_clock.Object);
        orchestrationInstance.Lifecycle.TransitionToRunning(_clock.Object);

        orchestrationInstance.TransitionStepToRunning(
            OrchestrationDescriptionBuilder.BusinessValidationStep,
            _clock.Object);
        orchestrationInstance.TransitionStepToTerminated(
            OrchestrationDescriptionBuilder.BusinessValidationStep,
            StepInstanceTerminationState.Succeeded,
            _clock.Object);

        // ForwardToMeasurements step is not transitioned to running.

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.OrchestrationInstances.Add(orchestrationInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        var act = () => Sut.HandleAsync(orchestrationInstance.Id);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);

        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleAsync(oi => oi.Id == orchestrationInstance.Id);

        // - The orchestration instance should not be changed.
        Assert.Equivalent(orchestrationInstance, actualOrchestrationInstance);

        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_OrchestrationInstanceDoesntExist_When_HandleAsync_Then_ThrowsException()
    {
        // Act
        var act = () => Sut.HandleAsync(new OrchestrationInstanceId(Guid.NewGuid()));

        // Assert
        await Assert.ThrowsAsync<NullReferenceException>(act);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    private OrchestrationInstance CreateOrchestrationInstance()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointId(_meteringPointId.Value)
            .WithActorMessageId(_actorMessageId)
            .WithMeteringPointType(MeteringPointType.Production.Name) // Used to determine the receivers
            .Build();

        var instance = OrchestrationInstance.CreateFromDescription(
            identity: new ActorIdentity(Actor.From(input.ActorNumber, input.ActorRole)),
            description: OrchestrationDescription,
            skipStepsBySequence: [],
            clock: _clock.Object,
            meteringPointId: new MeteringPointId(input.MeteringPointId!),
            actorMessageId: new ActorMessageId(input.ActorMessageId),
            transactionId: new TransactionId(input.TransactionId),
            idempotencyKey: IdempotencyKey.CreateNew());

        instance.ParameterValue.SetFromInstance(input);

        instance.CustomState.SetFromInstance(new ForwardMeteredDataCustomStateV2(
            HistoricalMeteringPointMasterData: [
                ForwardMeteredDataCustomStateV2.MasterData.FromMeteringPointMasterData(
                    new MeteringPointMasterDataBuilder().BuildFromInput(
                        input: input,
                        gridAccessProvider: _gridAccessProvider,
                        energySupplier: _energySupplier)),
            ],
            AdditionalRecipients: []));

        return instance;
    }

    private OrchestrationInstance CreateRunningOrchestrationInstance()
    {
        var instance = CreateOrchestrationInstance();
        instance.Lifecycle.TransitionToQueued(_clock.Object);
        instance.Lifecycle.TransitionToRunning(_clock.Object);

        instance.TransitionStepToRunning(
            OrchestrationDescriptionBuilder.BusinessValidationStep,
            _clock.Object);
        instance.TransitionStepToTerminated(
            OrchestrationDescriptionBuilder.BusinessValidationStep,
            StepInstanceTerminationState.Succeeded,
            _clock.Object);

        instance.TransitionStepToRunning(
            OrchestrationDescriptionBuilder.ForwardToMeasurementsStep,
            _clock.Object);

        return instance;
    }

    private OrchestrationInstance CreateTerminatedOrchestrationInstance()
    {
        var instance = CreateOrchestrationInstance();
        instance.Lifecycle.TransitionToQueued(_clock.Object);
        instance.Lifecycle.TransitionToRunning(_clock.Object);
        instance.Lifecycle.TransitionToSucceeded(_clock.Object);

        return instance;
    }

    private async Task<OrchestrationDescription> CreateSendMeasurementsOrchestrationDescriptionAsync()
    {
        await using var setupContext = _fixture.DatabaseManager.CreateDbContext();

        // Disable all existing orchestration descriptions to ensure that only the one we add is used
        var existingOrchestrationDescriptions = await setupContext.OrchestrationDescriptions
            .ToListAsync();
        existingOrchestrationDescriptions.ForEach(od => od.IsEnabled = false);

        // Create a new orchestration description
        var orchestrationDescription = new OrchestrationDescriptionBuilder().Build();
        setupContext.OrchestrationDescriptions.Add(orchestrationDescription);

        await setupContext.SaveChangesAsync();

        return orchestrationDescription;
    }

    private bool HasCorrectReceivers(ForwardMeteredDataAcceptedV1 m)
    {
        // There is only one period in the test data, so there should only be one ReceiversWithMeteredData.
        if (m.ReceiversWithMeteredData.Count != 1)
            return false;

        var receiversWithMeteredData = m.ReceiversWithMeteredData.Single();

        // Metering point type is consumption, which means there should be two receivers (energy supplier and danish energy agency).
        var hasEnergySupplier = receiversWithMeteredData.Actors.Any(
            a =>
                a.ActorNumber == _energySupplier &&
                a.ActorRole == ActorRole.EnergySupplier);

        var hasDanishEnergyAgency = receiversWithMeteredData.Actors.Any(
            a =>
                a.ActorNumber == ActorNumber.Create(DataHubDetails.DanishEnergyAgencyNumber) &&
                a.ActorRole == ActorRole.DanishEnergyAgency);

        // There should be exactly two actors in the ReceiversWithMeteredData, one for the energy supplier and one
        // for the danish energy agency.
        return receiversWithMeteredData.Actors.Count == 2 &&
               hasEnergySupplier &&
               hasDanishEnergyAgency;
    }
}
