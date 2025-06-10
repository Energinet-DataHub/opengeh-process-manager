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
using System.Text.Json;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Extensions;
using Energinet.DataHub.ProcessManager.Core.Application.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.FeatureManagement;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Moq;
using NodaTime;
using TelemetryConfiguration = Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

[Collection(nameof(ProcessManagerAzuriteCollection))]
public class EnqueueMeasurementsHandlerV1Tests
    : IClassFixture<ProcessManagerDatabaseFixture>, IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly ProcessManagerAzuriteFixture _azuriteFixture;
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IFeatureManager> _featureManager = new();
    private readonly Mock<IEnqueueActorMessagesClient> _enqueueActorMessagesClient = new();

    private readonly Instant _now = Instant.FromUtc(2025, 06, 06, 13, 37);
    private readonly MeteringPointId _meteringPointId = new("123456789012345678");
    private readonly string _actorMessageId = Guid.NewGuid().ToString();
    private readonly ActorNumber _gridAccessProvider = ActorNumber.Create("1111111111111");
    private readonly ActorNumber _energySupplier = ActorNumber.Create("1111111111112");
    private readonly ServiceProvider _serviceProvider;

    public EnqueueMeasurementsHandlerV1Tests(ProcessManagerDatabaseFixture fixture, ProcessManagerAzuriteFixture azuriteFixture)
    {
        _fixture = fixture;
        _azuriteFixture = azuriteFixture;

        _clock.Setup(c => c.GetCurrentInstant()).Returns(_now);

        _featureManager
            .Setup(fm => fm.IsEnabledAsync(FeatureFlagNames.UseNewSendMeasurementsTable))
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddTransient<IFileStorageClient, ProcessManagerBlobFileStorageClient>();
        services.AddAzureClients(builder => builder
            .AddBlobServiceClient(_azuriteFixture.AzuriteManager.BlobStorageConnectionString)
            .WithName(ProcessManagerBlobFileStorageClient.ClientName));
        _serviceProvider = services.BuildServiceProvider();
    }

    [NotNull]
    private ProcessManagerContext? DbContext { get; set; }

    [NotNull]
    private EnqueueMeasurementsHandlerV1? Sut { get; set; }

    public async Task InitializeAsync()
    {
        DbContext = _fixture.DatabaseManager.CreateDbContext();

        Sut = new EnqueueMeasurementsHandlerV1(
            new OrchestrationInstanceRepository(DbContext),
            new SendMeasurementsInstanceRepository(
                DbContext,
                _serviceProvider.GetRequiredService<IFileStorageClient>()),
            _clock.Object,
            _enqueueActorMessagesClient.Object,
            new MeteringPointReceiversProvider(DateTimeZone.Utc),
            new TelemetryClient(new TelemetryConfiguration
            {
                TelemetryChannel = Mock.Of<ITelemetryChannel>(),
            }),
            _featureManager.Object);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (DbContext != null) // DbContext can be null if InitializeAsync fails
            await DbContext.DisposeAsync();

        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Given_RunningInstance_When_HandleAsync_Then_ActorMessagesAreEnqueued()
    {
        // Arrange
        var (instance, inputStream, input) = await CreateRunningSendMeasurementsInstanceAsync();

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var repository = new SendMeasurementsInstanceRepository(
                setupContext,
                _serviceProvider.GetRequiredService<IFileStorageClient>());
            await repository.AddAsync(instance, inputStream);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(instance.Id.Value);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualInstance = await assertionDbContext.SendMeasurementsInstances
            .SingleAsync(oi => oi.Id == instance.Id);

        // - ForwardToMeasurements step should be terminated with success.
        // - EnqueueActorMessages step should be running.
        Assert.Multiple(
            () => Assert.True(actualInstance.IsReceivedFromMeasurements),
            () => Assert.Equal(_now, actualInstance.ReceivedFromMeasurementsAt),
            () => Assert.True(actualInstance.IsSentToEnqueueActorMessages),
            () => Assert.Equal(_now, actualInstance.SentToEnqueueActorMessagesAt));

        _enqueueActorMessagesClient.Verify(
            client => client.EnqueueAsync(
                Brs_021_ForwardedMeteredData.V1,
                instance.Id.Value,
                new ActorIdentityDto(actualInstance.CreatedByActorNumber, actualInstance.CreatedByActorRole),
                It.IsAny<Guid>(),
                It.Is<ForwardMeteredDataAcceptedV1>(m =>
                    m.MeteringPointId == _meteringPointId.Value &&
                    m.OriginalActorMessageId == _actorMessageId &&
                    HasCorrectReceiversWithMeteredData(m, input))),
            Times.Once);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_RunningInstance_AndGiven_AlreadyHasEnqueueIdempotencyKey_When_HandleAsync_Then_ActorMessagesAreEnqueuedWithSameIdempotencyKey()
    {
        // Arrange
        var (instance, inputStream, input) = await CreateRunningSendMeasurementsInstanceAsync();

        // Send Measurements Instance uses it's instance id as the idempotency key, so it should always be the same
        // idempotency key for the same instance.
        var enqueueIdempotencyKey = instance.Id.Value;

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var repository = new SendMeasurementsInstanceRepository(
                setupContext,
                _serviceProvider.GetRequiredService<IFileStorageClient>());
            await repository.AddAsync(instance, inputStream);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(instance.Id.Value);

        // Assert
        _enqueueActorMessagesClient.Verify(
            client => client.EnqueueAsync(
                Brs_021_ForwardedMeteredData.V1,
                instance.Id.Value,
                new ActorIdentityDto(instance.CreatedByActorNumber, instance.CreatedByActorRole),
                enqueueIdempotencyKey,
                It.IsAny<ForwardMeteredDataAcceptedV1>()),
            Times.Once);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_InstanceStuckAtEnqueueActorMessages_When_HandleAsync_Then_ActorMessagesAreEnqueued()
    {
        // Arrange
        var (instance, inputStream, input) = await CreateRunningSendMeasurementsInstanceAsync();

        // Simulate that the instance has terminated the ForwardToMeasurementsStep and
        // is stuck at the EnqueueActorMessages step
        instance.MarkAsReceivedFromMeasurements(_now);

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var repository = new SendMeasurementsInstanceRepository(
                setupContext,
                _serviceProvider.GetRequiredService<IFileStorageClient>());
            await repository.AddAsync(instance, inputStream);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(instance.Id.Value);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualInstance = await assertionDbContext.SendMeasurementsInstances
            .SingleAsync(oi => oi.Id == instance.Id);

        // - EnqueueActorMessages step should be running.
        Assert.True(actualInstance.IsSentToEnqueueActorMessages);

        // - Enqueue actor messages client should be called with the correct parameters.
        _enqueueActorMessagesClient.Verify(
            client => client.EnqueueAsync(
                Brs_021_ForwardedMeteredData.V1,
                instance.Id.Value,
                new ActorIdentityDto(instance.CreatedByActorNumber, instance.CreatedByActorRole),
                It.IsAny<Guid>(),
                It.Is<ForwardMeteredDataAcceptedV1>(m =>
                    m.MeteringPointId == _meteringPointId.Value &&
                    m.OriginalActorMessageId == _actorMessageId &&
                    HasCorrectReceiversWithMeteredData(m, input))),
            Times.Once);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_TerminatedInstance_When_HandleAsync_Then_NothingHappens()
    {
        // Arrange
        var (instance, inputStream, input) = await CreateTerminatedSendMeasurementsInstanceAsync();

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var repository = new SendMeasurementsInstanceRepository(
                setupContext,
                _serviceProvider.GetRequiredService<IFileStorageClient>());
            await repository.AddAsync(instance, inputStream);
            await setupContext.SaveChangesAsync();
        }

        // Act
        await Sut.HandleAsync(instance.Id.Value);

        // Assert
        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualInstance = await assertionDbContext.SendMeasurementsInstances
            .SingleAsync(oi => oi.Id == instance.Id);

        // - The instance should not be changed.
        Assert.Equivalent(instance, actualInstance);

        // - The enqueue actor messages client should not be called.
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_IsNotSentToMeasurements_When_HandleAsync_Then_ThrowsException_AndThen_ActorMessagesNotEnqueued()
    {
        // Arrange
        var (instance, inputStream, input) = await CreateSendMeasurementsInstanceAsync();
        instance.MarkAsBusinessValidationSucceeded(_now);
        // SentToMeasurements is not set.

        await using (var setupContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var repository = new SendMeasurementsInstanceRepository(
                setupContext,
                _serviceProvider.GetRequiredService<IFileStorageClient>());
            await repository.AddAsync(instance, inputStream);
            await setupContext.SaveChangesAsync();
        }

        // Act
        var act = () => Sut.HandleAsync(instance.Id.Value);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);

        await using var assertionDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualInstance = await assertionDbContext.SendMeasurementsInstances
            .SingleAsync(oi => oi.Id == instance.Id);

        // - The instance should not be changed.
        Assert.Equivalent(instance, actualInstance);

        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_InstanceDoesntExist_When_HandleAsync_Then_ThrowsException()
    {
        // Act
        var act = () => Sut.HandleAsync(Guid.NewGuid());

        // Assert
        await Assert.ThrowsAsync<NullReferenceException>(act);
        _enqueueActorMessagesClient.VerifyNoOtherCalls();
    }

    private async Task<(
        SendMeasurementsInstance Instance,
        Stream InputStream,
        ForwardMeteredDataInputV1 Input)> CreateSendMeasurementsInstanceAsync()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointId(_meteringPointId.Value)
            .WithActorMessageId(_actorMessageId)
            .WithMeteringPointType(MeteringPointType.Production.Name) // Used to determine the receivers
            .Build();

        var instance = new SendMeasurementsInstance(
            createdAt: _clock.Object.GetCurrentInstant(),
            createdBy: Actor.From(input.ActorNumber, input.ActorRole),
            transactionId: new TransactionId(input.TransactionId),
            meteringPointId: _meteringPointId,
            idempotencyKey: IdempotencyKey.CreateNew());

        instance.MasterData.SetFromInstance(new ForwardMeteredDataCustomStateV2(
            HistoricalMeteringPointMasterData: [
                ForwardMeteredDataCustomStateV2.MasterData.FromMeteringPointMasterData(
                    new MeteringPointMasterDataBuilder().BuildFromInput(
                        input: input,
                        gridAccessProvider: _gridAccessProvider,
                        energySupplier: _energySupplier)),
            ],
            AdditionalRecipients: []));

        var inputAsStream = await input.SerializeToStreamAsync();

        return (instance, inputAsStream, input);
    }

    private async Task<(
        SendMeasurementsInstance Instance,
        Stream InputStream,
        ForwardMeteredDataInputV1 Input)> CreateRunningSendMeasurementsInstanceAsync()
    {
        var result = await CreateSendMeasurementsInstanceAsync();
        result.Instance.MarkAsBusinessValidationSucceeded(_now);
        result.Instance.MarkAsSentToMeasurements(_now);

        return result;
    }

    private async Task<(
        SendMeasurementsInstance Instance,
        Stream InputStream,
        ForwardMeteredDataInputV1 Input)> CreateTerminatedSendMeasurementsInstanceAsync()
    {
        var result = await CreateSendMeasurementsInstanceAsync();
        result.Instance.MarkAsTerminated(_now);

        return result;
    }

    private bool HasCorrectReceiversWithMeteredData(ForwardMeteredDataAcceptedV1 acceptedMessage, ForwardMeteredDataInputV1 input)
    {
        // There is only one period in the test data, so there should only be one ReceiversWithMeteredData.
        if (acceptedMessage.ReceiversWithMeteredData.Count != 1)
            return false;

        var receiversWithMeteredData = acceptedMessage.ReceiversWithMeteredData.Single();

        // Metering point type is consumption, which means there should be two receivers (energy supplier and danish energy agency).
        var hasCorrectActorsCount = receiversWithMeteredData.Actors.Count == 2;
        var hasEnergySupplier = receiversWithMeteredData.Actors.Any(
            a =>
                a.ActorNumber == _energySupplier &&
                a.ActorRole == ActorRole.EnergySupplier);
        var hasDanishEnergyAgency = receiversWithMeteredData.Actors.Any(
            a =>
                a.ActorNumber == ActorNumber.Create(DataHubDetails.DanishEnergyAgencyNumber) &&
                a.ActorRole == ActorRole.DanishEnergyAgency);

        // Has correct metered data
        var hasCorrectMeteredDataCount = receiversWithMeteredData.MeteredData.Count == input.MeteredDataList.Count;
        var hasCorrectMeteredDataQuantity = receiversWithMeteredData.MeteredData.Sum(m => m.EnergyQuantity) ==
                                           input.MeteredDataList.Sum(m => decimal.Parse(m.EnergyQuantity!));
        var hasCorrectDates = acceptedMessage.StartDateTime == InstantPatternWithOptionalSeconds.Parse(input.StartDateTime).Value.ToDateTimeOffset()
            && acceptedMessage.EndDateTime == InstantPatternWithOptionalSeconds.Parse(input.EndDateTime!).Value.ToDateTimeOffset();

        return
            // Correct actors (receivers)
            hasCorrectActorsCount &&
            hasEnergySupplier &&
            hasDanishEnergyAgency &&

            // Correct metered data
            hasCorrectMeteredDataCount &&
            hasCorrectMeteredDataQuantity &&
            hasCorrectDates;
    }
}
