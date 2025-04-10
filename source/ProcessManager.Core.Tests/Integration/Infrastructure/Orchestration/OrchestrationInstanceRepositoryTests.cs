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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Moq;
using NodaTime;
using OrchestrationInstanceLifecycleState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceLifecycleState;
using OrchestrationInstanceTerminationState = Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance.OrchestrationInstanceTerminationState;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Orchestration;

public class OrchestrationInstanceRepositoryTests : IClassFixture<ProcessManagerCoreFixture>, IAsyncLifetime
{
    private readonly ProcessManagerCoreFixture _fixture;
    private readonly ProcessManagerContext _dbContext;
    private readonly OrchestrationInstanceRepository _sut;

    public OrchestrationInstanceRepositoryTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;
        _dbContext = _fixture.DatabaseManager.CreateDbContext();
        _sut = new OrchestrationInstanceRepository(_dbContext);
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
    public async Task Given_OrchestrationInstanceIdNotInDatabase_When_GetById_Then_ThrowsException()
    {
        // Arrange
        var id = new OrchestrationInstanceId(Guid.NewGuid());

        // Act
        var act = () => _sut.GetAsync(id);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Sequence contains no elements.");
    }

    [Fact]
    public async Task Given_OrchestrationInstanceIdInDatabase_When_GetById_Then_ExpectedOrchestrationInstanceIsRetrieved()
    {
        // Arrange
        var existingOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var existingOrchestrationInstance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.GetAsync(existingOrchestrationInstance.Id);

        // Assert
        actual.Should()
            .BeEquivalentTo(existingOrchestrationInstance);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceChangedFromMultipleConsumers_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
    {
        // Arrange
        var existingOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var existingOrchestrationInstance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // => First consumer (sut)
        var actual01 = await _sut.GetAsync(existingOrchestrationInstance.Id);
        actual01.Lifecycle.TransitionToQueued(SystemClock.Instance);

        // => Second consumer (sut02)
        using var dbContext02 = _fixture.DatabaseManager.CreateDbContext();
        var sut02 = new OrchestrationInstanceRepository(dbContext02);
        var actual02 = await sut02.GetAsync(existingOrchestrationInstance.Id);
        actual02.Lifecycle.TransitionToQueued(SystemClock.Instance);

        await _sut.UnitOfWork.CommitAsync();

        // Act
        var act = () => sut02.UnitOfWork.CommitAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Given_StepInstanceChangedFromMultipleConsumers_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
    {
        // Arrange
        var existingOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var existingOrchestrationInstance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // => First consumer (sut)
        var actual01 = await _sut.GetAsync(existingOrchestrationInstance.Id);
        actual01.TransitionStepToRunning(1, SystemClock.Instance);

        // => Second consumer (sut02)
        using var dbContext02 = _fixture.DatabaseManager.CreateDbContext();
        var sut02 = new OrchestrationInstanceRepository(dbContext02);
        var actual02 = await sut02.GetAsync(existingOrchestrationInstance.Id);
        actual02.TransitionStepToRunning(1, SystemClock.Instance);

        await _sut.UnitOfWork.CommitAsync();

        // Act
        var act = () => sut02.UnitOfWork.CommitAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Given_OrchestrationInstanceNotInDatabase_When_GetByIdempotencyKey_Then_ReturnsNull()
    {
        // Arrange
        var idempotencyKey = new IdempotencyKey(Guid.NewGuid().ToString());

        // Act
        var actual = await _sut.GetOrDefaultAsync(idempotencyKey);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public async Task Given_OrchestrationInstanceInDatabase_When_GetByIdempotencyKey_Then_ExpectedOrchestrationInstanceIsRetrieved()
    {
        // Arrange
        var existingOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var existingOrchestrationInstance = DomainTestDataFactory.CreateActorInitiatedOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: new IdempotencyKey(Guid.NewGuid().ToString()));

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.GetOrDefaultAsync(existingOrchestrationInstance.IdempotencyKey!);

        // Assert
        actual.Should()
            .BeEquivalentTo(existingOrchestrationInstance);
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionNotInDatabase_When_AddOrchestrationInstance_Then_ThrowsException()
    {
        // Arrange
        var newOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var newOrchestrationInstance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(newOrchestrationDescription);

        // Act
        await _sut.AddAsync(newOrchestrationInstance);
        var act = () => _sut.UnitOfWork.CommitAsync();

        // Assert
        await act.Should()
            .ThrowAsync<DbUpdateException>()
            .WithInnerException<DbUpdateException, SqlException>()
            .WithMessage("*FOREIGN KEY constraint \"FK_OrchestrationInstance_OrchestrationDescription\"*");
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionInDatabase_When_AddOrchestrationInstance_Then_OrchestrationInstanceIsAdded()
    {
        // Arrange
        var existingOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await writeDbContext.SaveChangesAsync();
        }

        var newOrchestrationInstance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription);

        // Act
        await _sut.AddAsync(newOrchestrationInstance);
        await _sut.UnitOfWork.CommitAsync();

        // Assert
        var actual = await _sut.GetAsync(newOrchestrationInstance.Id);
        actual.Should()
            .BeEquivalentTo(newOrchestrationInstance);
    }

    [Fact]
    public async Task Given_ScheduledOrchestrationInstancesInDatabase_When_GetScheduledByInstant_Then_ExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var currentInstant = SystemClock.Instance.GetCurrentInstant();

        var existingOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();

        var notScheduled = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription);
        var scheduledToRun = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(
            existingOrchestrationDescription,
            runAt: currentInstant.PlusMinutes(1));
        var scheduledIntoTheFarFuture = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(
            existingOrchestrationDescription,
            runAt: currentInstant.PlusDays(5));

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(notScheduled);
            writeDbContext.OrchestrationInstances.Add(scheduledToRun);
            writeDbContext.OrchestrationInstances.Add(scheduledIntoTheFarFuture);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.FindAsync(currentInstant.Plus(Duration.FromMinutes(5)));

        // Assert
        actual.Should()
            .ContainEquivalentOf(scheduledToRun)
            .And.NotContainEquivalentOf(notScheduled)
            .And.NotContainEquivalentOf(scheduledIntoTheFarFuture);
    }

    [Fact]
    public async Task Given_OrchestrationInstancesInDatabase_When_SearchByName_Then_ExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var uniqueName1 = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var existingOrchestrationDescription01 = DomainTestDataFactory.CreateOrchestrationDescription(uniqueName1);

        var uniqueName2 = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var existingOrchestrationDescription02 = DomainTestDataFactory.CreateOrchestrationDescription(uniqueName2);

        var basedOn01 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription01);
        var basedOn02 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription02);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription01);
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription02);
            writeDbContext.OrchestrationInstances.Add(basedOn01);
            writeDbContext.OrchestrationInstances.Add(basedOn02);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchAsync(existingOrchestrationDescription01.UniqueName.Name);

        // Assert
        actual.Should()
            .BeEquivalentTo(new[] { basedOn01 });
    }

    [Fact]
    public async Task Given_OrchestrationInstancesInDatabase_When_SearchByNameAndVersion_Then_ExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var name = Guid.NewGuid().ToString();
        var existingOrchestrationDescriptionV1 = DomainTestDataFactory.CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(name, 1));
        var existingOrchestrationDescriptionV2 = DomainTestDataFactory.CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(name, 2));

        var basedOnV1 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);
        var basedOnV2 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV2);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescriptionV1);
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescriptionV2);
            writeDbContext.OrchestrationInstances.Add(basedOnV1);
            writeDbContext.OrchestrationInstances.Add(basedOnV2);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchAsync(existingOrchestrationDescriptionV1.UniqueName.Name, existingOrchestrationDescriptionV1.UniqueName.Version);

        // Assert
        actual.Should()
            .BeEquivalentTo(new[] { basedOnV1 });
    }

    [Fact]
    public async Task Given_OrchestrationInstancesInDatabase_When_SearchByNameAndLifecycleState_Then_ExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var name = Guid.NewGuid().ToString();
        var existingOrchestrationDescriptionV1 = DomainTestDataFactory.CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(name, 1));
        var existingOrchestrationDescriptionV2 = DomainTestDataFactory.CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(name, 2));

        var isPendingV1 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);

        var isRunningV1 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);
        isRunningV1.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isRunningV1.Lifecycle.TransitionToRunning(SystemClock.Instance);

        var isPendingV2 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV2);

        var isRunningV2 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV2);
        isRunningV2.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isRunningV2.Lifecycle.TransitionToRunning(SystemClock.Instance);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescriptionV1);
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescriptionV2);
            writeDbContext.OrchestrationInstances.Add(isPendingV1);
            writeDbContext.OrchestrationInstances.Add(isRunningV1);
            writeDbContext.OrchestrationInstances.Add(isPendingV2);
            writeDbContext.OrchestrationInstances.Add(isRunningV2);
            await writeDbContext.SaveChangesAsync();
        }

        var lifecycleStates = new List<OrchestrationInstanceLifecycleState> { OrchestrationInstanceLifecycleState.Running };

        // Act
        var actual = await _sut.SearchAsync(existingOrchestrationDescriptionV1.UniqueName.Name, lifecycleStates: lifecycleStates);

        // Assert
        actual.Should()
            .BeEquivalentTo(new[] { isRunningV1, isRunningV2 });
    }

    [Fact]
    public async Task Given_OrchestrationInstancesInDatabase_When_SearchByNameAndTerminationState_Then_ExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var name = Guid.NewGuid().ToString();
        var existingOrchestrationDescriptionV1 = DomainTestDataFactory.CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(name, 1));
        var existingOrchestrationDescriptionV2 = DomainTestDataFactory.CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(name, 2));

        var isPendingV1 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);

        var isTerminatedAsSucceededV1 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);
        isTerminatedAsSucceededV1.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isTerminatedAsSucceededV1.Lifecycle.TransitionToRunning(SystemClock.Instance);
        isTerminatedAsSucceededV1.Lifecycle.TransitionToSucceeded(SystemClock.Instance);

        var isPendingV2 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV2);

        var isTerminatedAsFailedV2 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV2);
        isTerminatedAsFailedV2.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isTerminatedAsFailedV2.Lifecycle.TransitionToRunning(SystemClock.Instance);
        isTerminatedAsFailedV2.Lifecycle.TransitionToFailed(SystemClock.Instance);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescriptionV1);
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescriptionV2);
            writeDbContext.OrchestrationInstances.Add(isPendingV1);
            writeDbContext.OrchestrationInstances.Add(isTerminatedAsSucceededV1);
            writeDbContext.OrchestrationInstances.Add(isPendingV2);
            writeDbContext.OrchestrationInstances.Add(isTerminatedAsFailedV2);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchAsync(
            existingOrchestrationDescriptionV1.UniqueName.Name,
            lifecycleStates: [OrchestrationInstanceLifecycleState.Terminated],
            terminationState: OrchestrationInstanceTerminationState.Succeeded);

        // Assert
        actual.Should()
            .BeEquivalentTo(new[] { isTerminatedAsSucceededV1 });
    }

    [Fact]
    public async Task Given_OrchestrationInstancesInDatabase_When_SearchByNameAndStartedAt_Then_ExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var startedAt01 = SystemClock.Instance.GetCurrentInstant().PlusDays(1);
        var startedAtClockMock01 = new Mock<IClock>();
        startedAtClockMock01.Setup(m => m.GetCurrentInstant())
            .Returns(startedAt01);

        var name = Guid.NewGuid().ToString();
        var existingOrchestrationDescriptionV1 = DomainTestDataFactory.CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(name, 1));

        var isPending = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);

        var isRunning01 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);
        isRunning01.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isRunning01.Lifecycle.TransitionToRunning(startedAtClockMock01.Object);

        var isRunning02 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);
        isRunning02.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isRunning02.Lifecycle.TransitionToRunning(SystemClock.Instance);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescriptionV1);
            writeDbContext.OrchestrationInstances.Add(isPending);
            writeDbContext.OrchestrationInstances.Add(isRunning01);
            writeDbContext.OrchestrationInstances.Add(isRunning02);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchAsync(
            existingOrchestrationDescriptionV1.UniqueName.Name,
            startedAtOrLater: startedAt01);

        // Assert
        actual.Should()
            .BeEquivalentTo(new[] { isRunning01 });
    }

    [Fact]
    public async Task Given_OrchestrationInstancesInDatabase_When_SearchByNameAndTerminatedAt_Then_ExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var terminatedAt01 = SystemClock.Instance.GetCurrentInstant().PlusDays(-1);
        var terminatedAtClockMock01 = new Mock<IClock>();
        terminatedAtClockMock01.Setup(m => m.GetCurrentInstant())
            .Returns(terminatedAt01);

        var name = Guid.NewGuid().ToString();
        var existingOrchestrationDescriptionV1 = DomainTestDataFactory.CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(name, 1));

        var isPending = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);

        var isRunning = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);
        isRunning.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isRunning.Lifecycle.TransitionToRunning(SystemClock.Instance);

        var isTerminated01 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);
        isTerminated01.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isTerminated01.Lifecycle.TransitionToRunning(SystemClock.Instance);
        isTerminated01.Lifecycle.TransitionToSucceeded(terminatedAtClockMock01.Object);

        var isTerminated02 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescriptionV1);
        isTerminated02.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isTerminated02.Lifecycle.TransitionToRunning(SystemClock.Instance);
        isTerminated02.Lifecycle.TransitionToFailed(SystemClock.Instance);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescriptionV1);
            writeDbContext.OrchestrationInstances.Add(isPending);
            writeDbContext.OrchestrationInstances.Add(isRunning);
            writeDbContext.OrchestrationInstances.Add(isTerminated01);
            writeDbContext.OrchestrationInstances.Add(isTerminated02);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchAsync(
            existingOrchestrationDescriptionV1.UniqueName.Name,
            terminatedAtOrEarlier: terminatedAt01);

        // Assert
        actual.Should()
            .BeEquivalentTo(new[] { isTerminated01 });
    }

    [Fact]
    public async Task Given_OrchestrationInstancesInDatabase_When_SearchByActivatedWithinOneHourTomorrow_Then_ExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var now = SystemClock.Instance.GetCurrentInstant();

        var tomorrow = now.PlusDays(1);
        var tomorrowClockMock = new Mock<IClock>();
        tomorrowClockMock.Setup(m => m.GetCurrentInstant())
            .Returns(tomorrow);

        var dayAfterTomorrow = tomorrow.PlusDays(1);
        var dayAfterTomorrowClockMock = new Mock<IClock>();
        dayAfterTomorrowClockMock.Setup(m => m.GetCurrentInstant())
            .Returns(dayAfterTomorrow);

        // => Orchestration description 01
        var uniqueName01 = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var existingOrchestrationDescription01 = DomainTestDataFactory.CreateOrchestrationDescription(uniqueName01);

        var isPendingBasedOn01 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription01);

        var isQueuedNowBasedOn01 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription01);
        isQueuedNowBasedOn01.Lifecycle.TransitionToQueued(SystemClock.Instance);

        var isQueuedTomorrowBasedOn01 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription01);
        isQueuedTomorrowBasedOn01.Lifecycle.TransitionToQueued(tomorrowClockMock.Object);

        var isQueuedDayAfterTomorrowBasedOn01 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription01);
        isQueuedDayAfterTomorrowBasedOn01.Lifecycle.TransitionToQueued(dayAfterTomorrowClockMock.Object);

        // => Orchestration description 02
        var uniqueName02 = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var existingOrchestrationDescription02 = DomainTestDataFactory.CreateOrchestrationDescription(uniqueName02);

        var isScheduledToRunNowBasedOn02 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription02, runAt: now);

        var isScheduledToRunTomorrowBasedOn02 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription02, runAt: tomorrow);

        var isScheduledToRunDayAfterTomorrowBasedOn02 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription02, runAt: dayAfterTomorrow);

        // => Orchestration description 03
        var uniqueName03 = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var existingOrchestrationDescription03 = DomainTestDataFactory.CreateOrchestrationDescription(uniqueName03);

        var isQueuedNowBasedOn03 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription03);
        isQueuedNowBasedOn03.Lifecycle.TransitionToQueued(SystemClock.Instance);

        var isQueuedTomorrowBasedOn03 = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(existingOrchestrationDescription03);
        isQueuedTomorrowBasedOn03.Lifecycle.TransitionToQueued(tomorrowClockMock.Object);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription01);
            writeDbContext.OrchestrationInstances.Add(isPendingBasedOn01);
            writeDbContext.OrchestrationInstances.Add(isQueuedNowBasedOn01);
            writeDbContext.OrchestrationInstances.Add(isQueuedTomorrowBasedOn01);
            writeDbContext.OrchestrationInstances.Add(isQueuedDayAfterTomorrowBasedOn01);

            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription02);
            writeDbContext.OrchestrationInstances.Add(isScheduledToRunNowBasedOn02);
            writeDbContext.OrchestrationInstances.Add(isScheduledToRunTomorrowBasedOn02);
            writeDbContext.OrchestrationInstances.Add(isScheduledToRunDayAfterTomorrowBasedOn02);

            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription03);
            writeDbContext.OrchestrationInstances.Add(isQueuedNowBasedOn03);
            writeDbContext.OrchestrationInstances.Add(isQueuedTomorrowBasedOn03);

            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchAsync(
            orchestrationDescriptionNames: [uniqueName01.Name, uniqueName02.Name],
            activatedAtOrLater: tomorrow,
            activatedAtOrEarlier: tomorrow.PlusHours(1),
            createdByActorNumber: null,
            createdByActorRole: null);

        // Assert
        actual.Should()
            .BeEquivalentTo([
                (uniqueName01, isQueuedTomorrowBasedOn01),
                (uniqueName02, isScheduledToRunTomorrowBasedOn02)]);
    }

    [Fact]
    public async Task Given_TwoOrchestrationInstancesCreatedByDifferentActors_When_SearchWithCreatedByActor_Then_OnlyOneExpectedOrchestrationInstanceRetrieved()
    {
        // Arrange
        var now = SystemClock.Instance.GetCurrentInstant();
        var nowClockMock = new Mock<IClock>();
        nowClockMock.Setup(m => m.GetCurrentInstant())
            .Returns(now);

        var actorIdentity = DomainTestDataFactory.EnergySupplier.ActorIdentity;
        var otherActorIdentity = DomainTestDataFactory.BalanceResponsibleParty.ActorIdentity;

        var uniqueName = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var existingOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription(uniqueName);

        var expectedOrchestrationInstance = DomainTestDataFactory.CreateActorInitiatedOrchestrationInstance(
            existingOrchestrationDescription,
            createdByActorIdentity: actorIdentity);
        expectedOrchestrationInstance.Lifecycle.TransitionToQueued(nowClockMock.Object);

        var orchestrationInstanceCreatedByOtherActor = DomainTestDataFactory.CreateActorInitiatedOrchestrationInstance(
            existingOrchestrationDescription,
            createdByActorIdentity: otherActorIdentity);
        orchestrationInstanceCreatedByOtherActor.Lifecycle.TransitionToQueued(nowClockMock.Object);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(expectedOrchestrationInstance);
            writeDbContext.OrchestrationInstances.Add(orchestrationInstanceCreatedByOtherActor);

            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchAsync(
            orchestrationDescriptionNames: [uniqueName.Name],
            activatedAtOrLater: now,
            activatedAtOrEarlier: now,
            createdByActorNumber: actorIdentity.Actor.Number,
            createdByActorRole: actorIdentity.Actor.Role);

        // Assert
        actual.Should()
            .BeEquivalentTo([
                (uniqueName01: uniqueName, expectedOrchestrationInstance),
            ]);

        // This is also tested by the statement above, but this makes what we are testing explicit.
        actual.Should().NotContain((uniqueName, orchestrationInstanceCreatedByOtherActor));
    }

    [Fact]
    public async Task Given_TwoOrchestrationInstancesCreatedByDifferentActors_When_SearchWithoutCreatedByActor_Then_BothExpectedOrchestrationInstanceRetrieved()
    {
        // Arrange
        var now = SystemClock.Instance.GetCurrentInstant();
        var nowClockMock = new Mock<IClock>();
        nowClockMock.Setup(m => m.GetCurrentInstant())
            .Returns(now);

        var uniqueName = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var existingOrchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription(uniqueName);

        var actorIdentity1 = DomainTestDataFactory.EnergySupplier.ActorIdentity;
        var orchestrationInstanceByActor1 = DomainTestDataFactory.CreateActorInitiatedOrchestrationInstance(
            existingOrchestrationDescription,
            createdByActorIdentity: actorIdentity1);
        orchestrationInstanceByActor1.Lifecycle.TransitionToQueued(nowClockMock.Object);

        var actorIdentity2 = DomainTestDataFactory.BalanceResponsibleParty.ActorIdentity;
        var orchestrationInstanceByActor2 = DomainTestDataFactory.CreateActorInitiatedOrchestrationInstance(
            existingOrchestrationDescription,
            createdByActorIdentity: actorIdentity2);
        orchestrationInstanceByActor2.Lifecycle.TransitionToQueued(nowClockMock.Object);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(orchestrationInstanceByActor1);
            writeDbContext.OrchestrationInstances.Add(orchestrationInstanceByActor2);

            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchAsync(
            orchestrationDescriptionNames: [uniqueName.Name],
            activatedAtOrLater: now,
            activatedAtOrEarlier: now,
            createdByActorNumber: null,
            createdByActorRole: null);

        // Assert
        actual.Should()
            .BeEquivalentTo([
                (uniqueName01: uniqueName, orchestrationInstanceByActor1),
                (uniqueName01: uniqueName, orchestrationInstanceByActor2),
            ]);
    }
}
