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

using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Application.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Domain.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Orchestration;

public class SendMeasurementsInstanceRepositoryTests : IClassFixture<ProcessManagerCoreFixture>, IAsyncLifetime
{
    private readonly ProcessManagerCoreFixture _fixture;
    private readonly ProcessManagerContext _dbContext;
    private readonly IFileStorageClient _fileStorageClient;
    private readonly SendMeasurementsInstanceRepository _sut;

    public SendMeasurementsInstanceRepositoryTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;
        _dbContext = _fixture.DatabaseManager.CreateDbContext();

        var services = new ServiceCollection();
        services.AddTransient<IFileStorageClient, BlobFileStorageClient>();
        services.AddAzureClients(builder => builder
            .AddBlobServiceClient(_fixture.AzuriteManager.FullConnectionString)
            .WithName(BlobFileStorageClient.ClientName));
        var serviceProvider = services.BuildServiceProvider();
        _fileStorageClient = serviceProvider.GetRequiredService<IFileStorageClient>();

        _sut = new SendMeasurementsInstanceRepository(_dbContext, _fileStorageClient);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Given_SendMeasurementsInstanceNotInDatabase_When_GetById_Then_ThrowsException()
    {
        // Arrange
        var id = new SendMeasurementsInstanceId(Guid.NewGuid());

        // Act
        var act = () => _sut.GetAsync(id);

        // Assert
        await act.Should()
            .ThrowAsync<NullReferenceException>()
            .WithMessage("*SendMeasurementsInstance not found*");
    }

    [Fact]
    public async Task Given_SendMeasurementsInstanceInDatabase_When_GetById_Then_ExpectedInstanceIsRetrieved()
    {
        // Arrange
        var instance = CreateSendMeasurementsInstance();

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.SendMeasurementsInstances.Add(instance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.GetAsync(instance.Id);

        // Assert
        actual.Should().BeEquivalentTo(instance);
    }

    [Fact]
    public async Task Given_SendMeasurementsInstanceInDatabase_When_InstanceIsUpdated_Then_InstanceIsUpdatedInDatabase()
    {
        // Arrange
        var instance = CreateSendMeasurementsInstance();

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.SendMeasurementsInstances.Add(instance);
            await writeDbContext.SaveChangesAsync();
        }

        var sentToMeasurementsAt = Instant.FromUtc(2025, 01, 01, 01, 01);

        // Act
        var instanceToUpdate = await _sut.GetAsync(instance.Id);
        instanceToUpdate.MarkAsSentToMeasurements(sentToMeasurementsAt);

        await _sut.UnitOfWork.CommitAsync();

        // Assert
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var repository = new SendMeasurementsInstanceRepository(readDbContext, _fileStorageClient);
        var actual = await repository.GetAsync(instance.Id);
        actual.SentToMeasurementsAt.Should().Be(sentToMeasurementsAt);
    }

    [Fact]
    public async Task Given_SendMeasurementsInstanceChangedFromMultipleConsumers_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
    {
        // Arrange
        var instance = CreateSendMeasurementsInstance();

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.SendMeasurementsInstances.Add(instance);
            await writeDbContext.SaveChangesAsync();
        }

        // => First consumer (sut)
        var actual01 = await _sut.GetAsync(instance.Id);
        actual01.MarkAsSentToMeasurements(Instant.FromUtc(2025, 01, 01, 01, 01));

        // => Second consumer (sut02)
        await using var dbContext02 = _fixture.DatabaseManager.CreateDbContext();
        var sut02 = new SendMeasurementsInstanceRepository(dbContext02, _fileStorageClient);
        var actual02 = await sut02.GetAsync(instance.Id);
        actual02.MarkAsSentToMeasurements(Instant.FromUtc(2026, 02, 02, 02, 02));

        await _sut.UnitOfWork.CommitAsync();

        // Act
        var act = () => sut02.UnitOfWork.CommitAsync();

        // Assert
        await using var dbContext03 = _fixture.DatabaseManager.CreateDbContext();
        var sut03 = new SendMeasurementsInstanceRepository(dbContext03, _fileStorageClient);
        var actual03 = await sut03.GetAsync(instance.Id);
        actual03.Should().NotBeNull();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Given_SendMeasurementsInstanceInDatabase_When_GetByTransactionId_Then_ExpectedInstanceIsRetrieved()
    {
        // Arrange
        var instance = CreateSendMeasurementsInstance();

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.SendMeasurementsInstances.Add(instance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.GetOrDefaultAsync(instance.TransactionId);

        // Assert
        actual.Should().BeEquivalentTo(instance);
    }

    [Fact]
    public async Task Given_SendMeasurementsInstanceNotInDatabase_When_GetByTransactionId_Then_ReturnsNull()
    {
        // Arrange
        var transactionId = new TransactionId(Guid.NewGuid().ToString());

        // Act
        var actual = await _sut.GetOrDefaultAsync(transactionId);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public async Task When_AddSendMeasurementsInstance_Then_InstanceIsAddedToDatabase()
    {
        // Arrange
        var newInstance = CreateSendMeasurementsInstance();

        // Act
        await _sut.AddAsync(newInstance, new MemoryStream());
        await _sut.UnitOfWork.CommitAsync();

        // Assert
        var actual = await _sut.GetAsync(newInstance.Id);
        actual.Should()
            .BeEquivalentTo(newInstance);
    }

    [Fact]
    public async Task When_AddSendMeasurementsInstance_Then_InputIsAddedToFileStorage()
    {
        // Arrange
        var newInstance = CreateSendMeasurementsInstance();

        const string expectedInputContent = "Test content";
        var inputAsStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(inputAsStream, expectedInputContent);

        // Act
        await _sut.AddAsync(newInstance, inputAsStream);

        // Assert
        var fileContent = await DownloadFileContent(newInstance.FileStorageReference);

        var stringContent = fileContent.ToObjectFromJson<string>();
        stringContent.Should().Be(expectedInputContent);
    }

    private SendMeasurementsInstance CreateSendMeasurementsInstance()
    {
        return new SendMeasurementsInstance(
            SystemClock.Instance.GetCurrentInstant(),
            new Actor(ActorNumber.Create("1234567890123"), ActorRole.GridAccessProvider),
            new TransactionId(Guid.NewGuid().ToString()),
            new MeteringPointId("test-metering-point-id"));
    }

    private async Task<BinaryData> DownloadFileContent(FileStorageReference fileStorageReference)
    {
        var blobServiceClient = new BlobServiceClient(_fixture.AzuriteManager.FullConnectionString);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(fileStorageReference.Category);
        var blobClient = blobContainerClient.GetBlobClient(fileStorageReference.Path);
        var fileContent = await blobClient.DownloadContentAsync();
        return fileContent.Value.Content;
    }
}
