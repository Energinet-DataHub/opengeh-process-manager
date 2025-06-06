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
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Core.Application.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.FeatureManagement;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Moq;
using NodaTime;
using Actor = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.Actor;
using MeteringPointId = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.MeteringPointId;
using OrchestrationInstanceLifecycleState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceLifecycleState;
using TelemetryConfiguration = Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class StartForwardMeteredDataHandlerV1Tests
    : IClassFixture<ProcessManagerDatabaseFixture>, IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly ServiceProvider _serviceProvider;

    private readonly Mock<ILogger<StartForwardMeteredDataHandlerV1>> _logger = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IOptions<ProcessManagerOptions>> _options = new();
    private readonly Mock<IFeatureManager> _featureManager = new();
    private readonly Mock<IMeasurementsClient> _measurementsClient = new();
    private readonly Mock<IMeteringPointMasterDataProvider> _meteringPointMasterDataProvider = new();
    private readonly Mock<IEnqueueActorMessagesClient> _enqueueActorMessagesClient = new();
    private readonly Mock<IElectricityMarketViews> _electricityMarketViews = new();

    private readonly Actor _actor = new(ActorNumber.Create("1234567890123"), ActorRole.GridAccessProvider);
    private readonly MeteringPointId _meteringPointId = new("123456789012345678");
    private readonly ActorMessageId _actorMessageId = new(Guid.NewGuid().ToString());
    private readonly TransactionId _transactionId = new(Guid.NewGuid().ToString());

    public StartForwardMeteredDataHandlerV1Tests(ProcessManagerDatabaseFixture fixture)
    {
        _fixture = fixture;

        _options.Setup(o => o.Value)
            .Returns(new ProcessManagerOptions
            {
                AllowStartingOrchestrationsUnderDevelopment = true,
            });

        _featureManager.Setup(fm => fm.IsEnabledAsync(FeatureFlagNames.UseNewSendMeasurementsTable))
            .ReturnsAsync(true);

        _serviceProvider = new ServiceCollection()
            .AddNodaTimeForApplication()
            .AddBusinessValidation([typeof(Program).Assembly])
            .BuildServiceProvider();
    }

    [NotNull]
    private ProcessManagerContext? DbContext { get; set; }

    [NotNull]
    private StartForwardMeteredDataHandlerV1? Sut { get; set; }

    public async Task InitializeAsync()
    {
        DbContext = _fixture.DatabaseManager.CreateDbContext();

        Sut = CreateStartForwardMeteredDataHandlerV1();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (DbContext != null) // DbContext can be null if InitializeAsync fails
            await DbContext.DisposeAsync();

        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Given_ValidInput_When_Handled_Then_IsSentToMeasurements()
    {
        // Arrange
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointId(_meteringPointId.Value)
            .Build();

        _meteringPointMasterDataProvider
            .Setup(mpmdp => mpmdp.GetMasterData(_meteringPointId.Value, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync([new MeteringPointMasterDataBuilder().BuildFromInput(input)])
            .Verifiable(Times.Once);

        var idempotencyKey = IdempotencyKey.CreateNew();

        // Act
        await Sut.HandleAsync(CreateStartOrchestrationInstanceFromInput(input), idempotencyKey);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();

        var oldOrchestrationInstance = await assertionDbContext.OrchestrationInstances
            .SingleOrDefaultAsync(oi => oi.IdempotencyKey == idempotencyKey);

        var orchestrationInstance = await assertionDbContext.SendMeasurementsInstances
            .SingleOrDefaultAsync(smi => smi.IdempotencyKey == idempotencyKey.ToHash());

        Assert.NotNull(orchestrationInstance);
        Assert.Multiple(
            () => Assert.Null(oldOrchestrationInstance),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Running, orchestrationInstance.Lifecycle.State),
            () => Assert.True(orchestrationInstance.IsBusinessValidationSucceeded),
            () => Assert.True(orchestrationInstance.IsSentToMeasurements));

        // Measurements client should be called once
        _measurementsClient
            .Verify(
                mc => mc.SendAsync(
                    It.IsAny<MeasurementsForMeteringPoint>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(1));
        _measurementsClient.VerifyNoOtherCalls();

        // Metering point master data client should be called once (setup at the start of the test)
        _meteringPointMasterDataProvider.VerifyAll();
        _meteringPointMasterDataProvider.VerifyNoOtherCalls();

        // Enqueue actor messages client should not be called yet
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_InvalidInput_When_Handled_Then_BusinessValidationFailed_AndThen_RejectActorMessageIsEnqueued()
    {
        // Arrange
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointId(_meteringPointId.Value)
            .WithMeteredData([]) // Empty metered data will trigger validation error
            .Build();

        _meteringPointMasterDataProvider
            .Setup(mpmdp => mpmdp.GetMasterData(_meteringPointId.Value, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync([new MeteringPointMasterDataBuilder().BuildFromInput(input)])
            .Verifiable(Times.Once);

        var idempotencyKey = IdempotencyKey.CreateNew();

        // Act
        await Sut.HandleAsync(CreateStartOrchestrationInstanceFromInput(input), idempotencyKey);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();

        var orchestrationInstance = await assertionDbContext.SendMeasurementsInstances
            .SingleOrDefaultAsync(oi => oi.IdempotencyKey == idempotencyKey.ToHash());

        Assert.NotNull(orchestrationInstance);
        Assert.Multiple(
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Running, orchestrationInstance.Lifecycle.State),
            () => Assert.False(orchestrationInstance.IsBusinessValidationSucceeded),
            () => Assert.True(orchestrationInstance.IsBusinessValidationFailed),
            // Validation errors should be saved to the validation errors property
            () => Assert.NotEmpty(orchestrationInstance.ValidationErrors.SerializedValue),
            () => Assert.False(orchestrationInstance.IsSentToMeasurements, "Should not be sent to measurements when validation fails"),
            () => Assert.True(orchestrationInstance.IsSentToEnqueueActorMessagesAt, "Rejected actor message should be enqueued"));

        // Enqueue actor messages client should be called once to enqueue the rejected message
        _enqueueActorMessagesClient.Verify(
            eamc => eamc.EnqueueAsync(
                Brs_021_ForwardedMeteredData.V1,
                orchestrationInstance.Id.Value,
                new ActorIdentityDto(orchestrationInstance.CreatedByActorNumber, orchestrationInstance.CreatedByActorRole),
                It.IsAny<Guid>(),
                It.IsAny<ForwardMeteredDataRejectedV1>()),
            Times.Once);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();

        // Metering point master data provider should be called once
        _meteringPointMasterDataProvider.VerifyAll(); // Verify the mocked call from the test setup
        _meteringPointMasterDataProvider.VerifyNoOtherCalls();

        // Measurements client should not be called
        _measurementsClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_ExistingInstanceAlreadyTerminated_When_Handled_Then_NothingHappens()
    {
        // Arrange
        var idempotencyKey = IdempotencyKey.CreateNew();
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointId(_meteringPointId.Value)
            .Build();

        var existingInstance = CreateExistingInstance(idempotencyKey);
        existingInstance.MarkAsBusinessValidationSucceeded(_clock.Object.GetCurrentInstant());
        existingInstance.MarkAsSentToMeasurements(_clock.Object.GetCurrentInstant());
        existingInstance.MarkAsSentToEnqueueActorMessages(_clock.Object.GetCurrentInstant());
        existingInstance.MarkAsTerminated(_clock.Object.GetCurrentInstant());

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.SendMeasurementsInstances.Add(existingInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(CreateStartOrchestrationInstanceFromInput(input), idempotencyKey);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();

        var instances = await assertionDbContext.SendMeasurementsInstances
            .Where(oi => oi.IdempotencyKey == idempotencyKey.ToHash())
            .ToListAsync();

        // Only one instance should exist
        var instance = Assert.Single(instances);

        // The instance should be the already existing one
        Assert.Equal(existingInstance.Id, instance.Id);

        // No clients should be called
        _meteringPointMasterDataProvider.VerifyNoOtherCalls();
        _measurementsClient.VerifyNoOtherCalls();
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_ExistingInstanceStuckAtSendToMeasurements_When_Handled_Then_IsSentToMeasurements()
    {
        // Arrange
        var idempotencyKey = IdempotencyKey.CreateNew();
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointId(_meteringPointId.Value)
            .Build();

        var existingInstance = CreateExistingInstance(idempotencyKey);
        existingInstance.MarkAsBusinessValidationSucceeded(_clock.Object.GetCurrentInstant());

        // Set master data on custom state, since it should already be retrieved and set on the existing instance
        existingInstance.MasterData.SetFromInstance(new ForwardMeteredDataCustomStateV2(
            HistoricalMeteringPointMasterData: [
                ForwardMeteredDataCustomStateV2.MasterData.FromMeteringPointMasterData(
                    new MeteringPointMasterDataBuilder().BuildFromInput(input)),
                ],
            AdditionalRecipients: []));

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            setupContext.SendMeasurementsInstances.Add(existingInstance);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(CreateStartOrchestrationInstanceFromInput(input), idempotencyKey);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();

        var instances = await assertionDbContext.SendMeasurementsInstances
            .Where(oi => oi.IdempotencyKey == idempotencyKey.ToHash())
            .ToListAsync();

        // Only one instance should exist
        var instance = Assert.Single(instances);

        // The instance should be the already existing one
        Assert.Multiple(
            () => Assert.Equal(existingInstance.Id, instance.Id),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Running, instance.Lifecycle.State),
            () => Assert.True(instance.IsBusinessValidationSucceeded),
            () => Assert.Empty(instance.ValidationErrors.SerializedValue),
            () => Assert.NotNull(instance.SentToMeasurementsAt));

        // Measurements client should be called once
        _measurementsClient
            .Verify(
                mc => mc.SendAsync(
                    It.IsAny<MeasurementsForMeteringPoint>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(1));
        _measurementsClient.VerifyNoOtherCalls();

        // Master data is already set in custom state, so no calls should be made
        _meteringPointMasterDataProvider.VerifyNoOtherCalls();

        // Enqueue actor messages client should not be called yet
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    private StartOrchestrationInstanceV1 CreateStartOrchestrationInstanceFromInput(
        ForwardMeteredDataInputV1 input)
    {
        var startOrchestrationInstance = new StartOrchestrationInstanceV1
        {
            OrchestrationName = Brs_021_ForwardedMeteredData.V1.Name,
            OrchestrationVersion = Brs_021_ForwardedMeteredData.V1.Version,
            ActorMessageId = _actorMessageId.Value,
            TransactionId = _transactionId.Value,
            MeteringPointId = _meteringPointId.Value,
            StartedByActor = new StartOrchestrationInstanceActorV1
            {
                ActorNumber = _actor.Number.Value,
                ActorRole = _actor.Role.ToActorRoleV1(),
            },
        };

        startOrchestrationInstance.SetInput(input);

        return startOrchestrationInstance;
    }

    private SendMeasurementsInstance CreateExistingInstance(IdempotencyKey idempotencyKey)
    {
        var instance = new SendMeasurementsInstance(
            createdAt: _clock.Object.GetCurrentInstant(),
            createdBy: _actor,
            transactionId: _transactionId,
            meteringPointId: _meteringPointId,
            idempotencyKey: idempotencyKey);

        return instance;
    }

    private StartForwardMeteredDataHandlerV1 CreateStartForwardMeteredDataHandlerV1()
    {
        var businessValidator = _serviceProvider.GetRequiredService<BusinessValidator<ForwardMeteredDataBusinessValidatedDto>>();

        var orchestrationInstanceRepository = new OrchestrationInstanceRepository(DbContext);
        return new StartForwardMeteredDataHandlerV1(
            _logger.Object,
            new OrchestrationInstanceManager(
                _clock.Object,
                new DurableOrchestrationInstanceExecutor(
                    Mock.Of<ILogger<DurableOrchestrationInstanceExecutor>>(),
                    Mock.Of<IDurableClient>()),
                new OrchestrationRegister(
                    _options.Object,
                    Mock.Of<ILogger<OrchestrationRegister>>(),
                    DbContext),
                orchestrationInstanceRepository,
                _featureManager.Object,
                _options.Object,
                Mock.Of<ILogger<OrchestrationInstanceManager>>()),
            new SendMeasurementsInstanceRepository(DbContext, Mock.Of<IFileStorageClient>()),
            orchestrationInstanceRepository,
            _clock.Object,
            _measurementsClient.Object,
            businessValidator,
            _meteringPointMasterDataProvider.Object,
            Mock.Of<IAdditionalMeasurementsRecipientsProvider>(),
            _enqueueActorMessagesClient.Object,
            _featureManager.Object,
            new DelegationProvider(_electricityMarketViews.Object),
            new TelemetryClient(new TelemetryConfiguration
            {
                TelemetryChannel = Mock.Of<ITelemetryChannel>(),
            }));
    }
}
