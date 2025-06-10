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
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.FeatureManagement;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
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
public class TerminateForwardMeteredDataHandlerV1Tests
    : IClassFixture<ProcessManagerDatabaseFixture>, IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly ProcessManagerAzuriteFixture _azuriteFixture;

    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IFeatureManager> _featureManager = new();

    private readonly Instant _now = Instant.FromUtc(2025, 06, 06, 13, 37);
    private readonly ServiceProvider _serviceProvider;

    public TerminateForwardMeteredDataHandlerV1Tests(
        ProcessManagerDatabaseFixture fixture,
        ProcessManagerAzuriteFixture azuriteFixture)
    {
        _fixture = fixture;
        _azuriteFixture = azuriteFixture;

        _clock.Setup(c => c.GetCurrentInstant()).Returns(_now);

        _featureManager
            .Setup(fm => fm.IsEnabledAsync(FeatureFlagNames.UseNewSendMeasurementsTable))
            .ReturnsAsync(true);

        var services = new ServiceCollection();

        // File Storage
        services.AddTransient<IFileStorageClient, ProcessManagerBlobFileStorageClient>();
        services.AddAzureClients(builder => builder
            .AddBlobServiceClient(_azuriteFixture.AzuriteManager.BlobStorageConnectionString)
            .WithName(ProcessManagerBlobFileStorageClient.ClientName));
        _serviceProvider = services.BuildServiceProvider();
    }

    [NotNull]
    private ProcessManagerContext? DbContext { get; set; }

    [NotNull]
    private TerminateForwardMeteredDataHandlerV1? Sut { get; set; }

    public async Task InitializeAsync()
    {
        DbContext = _fixture.DatabaseManager.CreateDbContext();

        Sut = new TerminateForwardMeteredDataHandlerV1(
            new OrchestrationInstanceRepository(DbContext),
            new SendMeasurementsInstanceRepository(DbContext, _serviceProvider.GetRequiredService<IFileStorageClient>()),
            _clock.Object,
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
    public async Task Given_RunningOrchestrationInstance_When_HandleAsync_Then_OrchestrationInstanceIsTerminated()
    {
        // Arrange
        var (instance, inputStream) = await CreateRunningSendMeasurementsInstanceAsync();

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

        // - EnqueueActorMessages step should be terminated with success.
        // - SendMeasurementsInstance should be terminated with success.
        Assert.Multiple(
            () => Assert.True(actualInstance.IsReceivedFromEnqueueActorMessages),
            () => Assert.Equal(_now, actualInstance.ReceivedFromEnqueueActorMessagesAt),
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, actualInstance.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, actualInstance.Lifecycle.TerminationState),
            () => Assert.Equal(_now, actualInstance.TerminatedAt));
    }

    [Fact]
    public async Task Given_TerminatedOrchestrationInstance_When_HandleAsync_Then_NothingHappens()
    {
        // Arrange
        var (instance, inputStream) = await CreateTerminatedSendMeasurementsInstanceAsync();

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

        // - The orchestration instance should not be changed.
        Assert.Equivalent(instance, actualInstance);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceStuckAtTerminating_When_HandleAsync_Then_InstanceIsTerminated()
    {
        // Arrange
        var (instance, inputStream) = await CreateRunningSendMeasurementsInstanceAsync();

        // Terminate the EnqueueActorMessages step, but not the instance
        instance.MarkAsReceivedFromEnqueueActorMessages(_now);

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

        // - OrchestrationInstance should be terminated with success.
        Assert.Multiple(
            () => Assert.Equal(OrchestrationInstanceLifecycleState.Terminated, actualInstance.Lifecycle.State),
            () => Assert.Equal(OrchestrationInstanceTerminationState.Succeeded, actualInstance.Lifecycle.TerminationState),
            () => Assert.Equal(_now, actualInstance.TerminatedAt));
    }

    [Fact]
    public async Task Given_OrchestrationInstanceDoesntExist_When_HandleAsync_Then_ThrowsException()
    {
        // Act
        var act = () => Sut.HandleAsync(Guid.NewGuid());

        // Assert
        await Assert.ThrowsAsync<NullReferenceException>(act);
    }

    private async Task<(
        SendMeasurementsInstance Instance,
        Stream InputStream)> CreateSendMeasurementsInstanceAsync()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var instance = new SendMeasurementsInstance(
            createdAt: _clock.Object.GetCurrentInstant(),
            createdBy: Actor.From(input.ActorNumber, input.ActorRole),
            transactionId: new TransactionId(input.TransactionId),
            meteringPointId: new MeteringPointId(input.MeteringPointId!),
            idempotencyKey: IdempotencyKey.CreateNew());

        instance.MasterData.SetFromInstance(new ForwardMeteredDataCustomStateV2(
            HistoricalMeteringPointMasterData: [
                ForwardMeteredDataCustomStateV2.MasterData.FromMeteringPointMasterData(
                    new MeteringPointMasterDataBuilder().BuildFromInput(
                        input: input)),
            ],
            AdditionalRecipients: []));

        var inputAsStream = await input.SerializeToStreamAsync();

        return (instance, inputAsStream);
    }

    private async Task<(
        SendMeasurementsInstance Instance,
        Stream InputStream)> CreateRunningSendMeasurementsInstanceAsync()
    {
        var result = await CreateSendMeasurementsInstanceAsync();
        result.Instance.MarkAsBusinessValidationSucceeded(_now);
        result.Instance.MarkAsSentToMeasurements(_now);
        result.Instance.MarkAsReceivedFromMeasurements(_now);
        result.Instance.MarkAsSentToEnqueueActorMessages(_now);

        return result;
    }

    private async Task<(
        SendMeasurementsInstance Instance,
        Stream InputStream)> CreateTerminatedSendMeasurementsInstanceAsync()
    {
        var result = await CreateRunningSendMeasurementsInstanceAsync();
        result.Instance.MarkAsReceivedFromEnqueueActorMessages(_now);
        result.Instance.MarkAsTerminated(_now);

        return result;
    }
}
