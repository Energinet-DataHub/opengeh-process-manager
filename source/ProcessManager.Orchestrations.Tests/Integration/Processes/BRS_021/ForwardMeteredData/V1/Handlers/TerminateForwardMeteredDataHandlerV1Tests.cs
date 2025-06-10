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
using Energinet.DataHub.ProcessManager.Core.Application.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Moq;
using NodaTime;
using TelemetryConfiguration = Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class TerminateForwardMeteredDataHandlerV1Tests
    : IClassFixture<ProcessManagerDatabaseFixture>, IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IFeatureManager> _featureManager = new();
    private readonly Mock<IFileStorageClient> _fileStorageClient = new();

    private readonly Instant _now = Instant.FromUtc(2025, 06, 06, 13, 37);

    public TerminateForwardMeteredDataHandlerV1Tests(ProcessManagerDatabaseFixture fixture)
    {
        _fixture = fixture;

        _clock.Setup(c => c.GetCurrentInstant()).Returns(_now);
    }

    [NotNull]
    private ProcessManagerContext? DbContext { get; set; }

    [NotNull]
    private OrchestrationDescription? OrchestrationDescription { get; set; }

    [NotNull]
    private TerminateForwardMeteredDataHandlerV1? Sut { get; set; }

    public async Task InitializeAsync()
    {
        DbContext = _fixture.DatabaseManager.CreateDbContext();

        OrchestrationDescription = await CreateSendMeasurementsOrchestrationDescriptionAsync();

        Sut = new TerminateForwardMeteredDataHandlerV1(
            new OrchestrationInstanceRepository(DbContext),
            new SendMeasurementsInstanceRepository(DbContext, _fileStorageClient.Object),
            _clock.Object,
            new TelemetryClient(new TelemetryConfiguration
            {
                TelemetryChannel = Mock.Of<ITelemetryChannel>(),
            }),
            _featureManager.Object);
    }

    public async Task DisposeAsync()
    {
        if (DbContext != null) // DbContext can be null if InitializeAsync fails
            await DbContext.DisposeAsync();
    }

    [Fact]
    public async Task Given_RunningOrchestrationInstance_When_HandleAsync_Then_OrchestrationInstanceIsTerminated()
    {
        // Arrange
        var orchestrationInstance = CreateRunningOrchestrationInstance();

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.OrchestrationInstances.Add(orchestrationInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(orchestrationInstance.Id.Value);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleAsync(oi => oi.Id == orchestrationInstance.Id);

        var enqueueStep = actualOrchestrationInstance.GetStep(OrchestrationDescriptionBuilder.EnqueueActorMessagesStep);

        // - EnqueueActorMessages step should be terminated with success.
        // - OrchestrationInstance should be terminated with success.
        Assert.Multiple(
            () => Assert.Equal(StepInstanceLifecycleState.Terminated, enqueueStep.Lifecycle.State),
            () => Assert.Equal(StepInstanceTerminationState.Succeeded, enqueueStep.Lifecycle.TerminationState),
            () => Assert.Equal(_now, enqueueStep.Lifecycle.TerminatedAt),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, actualOrchestrationInstance.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, actualOrchestrationInstance.Lifecycle.TerminationState),
            () => Assert.Equal(_now, actualOrchestrationInstance.Lifecycle.TerminatedAt));
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
        await Sut.HandleAsync(orchestrationInstance.Id.Value);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleAsync(oi => oi.Id == orchestrationInstance.Id);

        // - The orchestration instance should not be changed.
        Assert.Equivalent(orchestrationInstance, actualOrchestrationInstance);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceStuckAtTerminating_When_HandleAsync_Then_InstanceIsTerminated()
    {
        // Arrange
        var orchestrationInstance = CreateRunningOrchestrationInstance();

        // Terminate the EnqueueActorMessages step, but not the orchestration instance
        orchestrationInstance.TransitionStepToTerminated(
            sequence: OrchestrationDescriptionBuilder.EnqueueActorMessagesStep,
            StepInstanceTerminationState.Succeeded,
            _clock.Object);

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.OrchestrationInstances.Add(orchestrationInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(orchestrationInstance.Id.Value);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleAsync(oi => oi.Id == orchestrationInstance.Id);

        // - OrchestrationInstance should be terminated with success.
        Assert.Multiple(
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, actualOrchestrationInstance.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, actualOrchestrationInstance.Lifecycle.TerminationState),
            () => Assert.Equal(_now, actualOrchestrationInstance.Lifecycle.TerminatedAt));
    }

    [Fact]
    public async Task Given_OrchestrationInstanceDoesntExist_When_HandleAsync_Then_ThrowsException()
    {
        // Act
        var act = () => Sut.HandleAsync(Guid.NewGuid());

        // Assert
        await Assert.ThrowsAsync<NullReferenceException>(act);
    }

    private OrchestrationInstance CreateOrchestrationInstance()
    {
        var input = new ForwardMeteredDataInputV1Builder()
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
                        input: input)),
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
        instance.TransitionStepToTerminated(
            OrchestrationDescriptionBuilder.ForwardToMeasurementsStep,
            StepInstanceTerminationState.Succeeded,
            _clock.Object);

        instance.TransitionStepToRunning(
            OrchestrationDescriptionBuilder.EnqueueActorMessagesStep,
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
}
