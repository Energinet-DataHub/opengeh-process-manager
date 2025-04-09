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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Database;

public class ProcessManagerContextTests : IClassFixture<ProcessManagerCoreFixture>
{
    private readonly ProcessManagerCoreFixture _fixture;
    private readonly UserIdentity _userIdentity = new UserIdentity(
        new UserId(Guid.NewGuid()),
        new Actor(ActorNumber.Create("1234567890123"), ActorRole.EnergySupplier));

    public ProcessManagerContextTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionAddedToDbContext_When_RetrievingFromDatabase_Then_HasCorrectValues()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationDescription = await readDbContext.OrchestrationDescriptions.FindAsync(existingOrchestrationDescription.Id);

        // Assert
        orchestrationDescription.Should()
            .NotBeNull()
            .And
            .BeEquivalentTo(existingOrchestrationDescription);
    }

    [Fact]
    public async Task Given_RecurringOrchestrationDescriptionAddedToDbContext_When_RetrievingFromDatabase_Then_HasCorrectValues()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription(recurringCronExpression: "0 0 * * *");

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationDescription = await readDbContext.OrchestrationDescriptions.FindAsync(existingOrchestrationDescription.Id);

        // Assert
        orchestrationDescription.Should()
            .NotBeNull()
            .And
            .BeEquivalentTo(existingOrchestrationDescription);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceWithStepsAddedToDbContext_When_RetrievingFromDatabase_Then_HasCorrectValues()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(existingOrchestrationDescription);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationInstance = await readDbContext.OrchestrationInstances.FindAsync(existingOrchestrationInstance.Id);

        // Assert
        orchestrationInstance.Should()
            .NotBeNull()
            .And
            .BeEquivalentTo(existingOrchestrationInstance);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceWithUniqueIdempotencyKeyAddedToDbContext_When_RetrievingFromDatabase_Then_HasCorrectValues()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: new IdempotencyKey(Guid.NewGuid().ToString()));

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationInstance = await readDbContext.OrchestrationInstances.FindAsync(existingOrchestrationInstance.Id);

        // Assert
        orchestrationInstance.Should()
            .NotBeNull()
            .And
            .BeEquivalentTo(existingOrchestrationInstance);
    }

    [Fact]
    public async Task Given_MultipleOrchestrationInstancesWithNullInIdempotencyKeyAddedToDbContext_When_SaveChangesAsync_Then_NoExceptionThrown()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var newOrchestrationInstance01 = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: null);
        var newOrchestrationInstance02 = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: null);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(newOrchestrationInstance01);
            writeDbContext.OrchestrationInstances.Add(newOrchestrationInstance02);
            // Act
            await writeDbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Given_MultipleOrchestrationInstancesWithSameValueInIdempotencyKeyAddedToDbContext_When_SaveChangesAsync_Then_ThrowsExpectedException()
    {
        // Arrange
        var idempotencyKey = new IdempotencyKey(Guid.NewGuid().ToString());
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var newOrchestrationInstance01 = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: idempotencyKey);
        var newOrchestrationInstance02 = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: idempotencyKey);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(newOrchestrationInstance01);
            writeDbContext.OrchestrationInstances.Add(newOrchestrationInstance02);
            // Act
            var act = () => writeDbContext.SaveChangesAsync();
            // Assert
            using var assertionScope = new AssertionScope();
            var ex = await act.Should()
                .ThrowAsync<DbUpdateException>();
            ex.Which!.InnerException!.Message
                .Contains("Cannot insert duplicate key row in object 'pm.OrchestrationInstance' with unique index 'UX_OrchestrationInstance_IdempotencyKey'");
        }
    }

    [Fact]
    public async Task Given_UserCanceledOrchestrationInstanceAddedToDbContext_When_RetrievingFromDatabase_Then_HasCorrectValues()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            identity: _userIdentity,
            runAt: SystemClock.Instance.GetCurrentInstant());
        existingOrchestrationInstance.Lifecycle.TransitionToUserCanceled(SystemClock.Instance, _userIdentity);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationInstance = await readDbContext.OrchestrationInstances.FindAsync(existingOrchestrationInstance.Id);

        // Assert
        orchestrationInstance.Should()
            .NotBeNull()
            .And
            .BeEquivalentTo(existingOrchestrationInstance);
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionChangedFromMultipleConsumer_AndGiven_ConsumerUsesOneDatabaseContext_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await writeDbContext.SaveChangesAsync();
        }

        await using var dbContext01 = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationDescriptionFromContext01 = await dbContext01.OrchestrationDescriptions.FindAsync(existingOrchestrationDescription.Id);
        orchestrationDescriptionFromContext01!.FunctionName = "First";

        await using var dbContext02 = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationDescriptionFromContext02 = await dbContext02.OrchestrationDescriptions.FindAsync(existingOrchestrationDescription.Id);
        orchestrationDescriptionFromContext02!.FunctionName = "Second";

        await dbContext01.SaveChangesAsync();

        // Act
        var act = () => dbContext02.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    /// <summary>
    /// Beware that we use Update on the database context (because we use one for reading and another for updating), which is why the
    /// OrchestrationDescription RowVersion is updated even if we change the steps.
    /// </summary>
    [Fact]
    public async Task Given_OrchestrationDescriptionChangedFromMultipleConsumer_AndGiven_ConsumerUsesTwoDatabaseContext_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await writeDbContext.SaveChangesAsync();
        }

        var orchestrationDescriptionFromContext01 = await ReadOrchestrationDescriptionAsync(existingOrchestrationDescription.Id);
        orchestrationDescriptionFromContext01.FunctionName = "First";

        var orchestrationDescriptionFromContext02 = await ReadOrchestrationDescriptionAsync(existingOrchestrationDescription.Id);
        orchestrationDescriptionFromContext02.FunctionName = "Second";

        await UpdateOrchestrationDescriptionAsync(orchestrationDescriptionFromContext01);

        // Act
        var act = () => UpdateOrchestrationDescriptionAsync(orchestrationDescriptionFromContext02);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Given_OrchestrationInstanceChangedFromMultipleConsumers_AndGiven_ConsumerUsesOneDatabaseContext_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(existingOrchestrationDescription);
        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        await using var dbContext01 = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationInstanceFromContext01 = await dbContext01.OrchestrationInstances.FindAsync(existingOrchestrationInstance.Id);
        orchestrationInstanceFromContext01!.Lifecycle.TransitionToQueued(SystemClock.Instance);

        await using var dbContext02 = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationInstanceFromContext02 = await dbContext02.OrchestrationInstances.FindAsync(existingOrchestrationInstance.Id);
        orchestrationInstanceFromContext02!.Lifecycle.TransitionToQueued(SystemClock.Instance);

        await dbContext01.SaveChangesAsync();

        // Act
        var act = () => dbContext02.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    /// <summary>
    /// Beware that we use Update on the database context (because we use one for reading and another for updating), which is why the
    /// OrchestrationInstance RowVersion is updated even if we change the steps.
    /// </summary>
    [Fact]
    public async Task Given_OrchestrationInstanceChangedFromMultipleConsumers_AndGiven_ConsumerUsesTwoDatabaseContext_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(existingOrchestrationDescription);
        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        var orchestrationInstanceFromContext01 = await ReadOrchestrationInstanceAsync(existingOrchestrationInstance.Id);
        orchestrationInstanceFromContext01.Lifecycle.TransitionToQueued(SystemClock.Instance);

        var orchestrationInstanceFromContext02 = await ReadOrchestrationInstanceAsync(existingOrchestrationInstance.Id);
        orchestrationInstanceFromContext02.Lifecycle.TransitionToQueued(SystemClock.Instance);

        await UpdateOrchestrationInstanceAsync(orchestrationInstanceFromContext01);

        // Act
        var act = () => UpdateOrchestrationInstanceAsync(orchestrationInstanceFromContext02);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static OrchestrationDescription CreateOrchestrationDescription(string? recurringCronExpression = default)
    {
        var orchestrationDescription = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName("TestOrchestration", 4),
            canBeScheduled: true,
            functionName: "TestOrchestrationFunction");

        if (recurringCronExpression != null)
            orchestrationDescription.RecurringCronExpression = recurringCronExpression;

        orchestrationDescription.ParameterDefinition.SetFromType<TestOrchestrationParameter>();

        orchestrationDescription.AppendStepDescription("Test step 1");
        orchestrationDescription.AppendStepDescription("Test step 2");
        orchestrationDescription.AppendStepDescription("Test step 3", canBeSkipped: true, skipReason: "Because we are testing");

        return orchestrationDescription;
    }

    private OrchestrationInstance CreateOrchestrationInstance(
        OrchestrationDescription orchestrationDescription,
        OperatingIdentity? identity = default,
        Instant? runAt = default,
        int? testInt = default,
        IdempotencyKey? idempotencyKey = default)
    {
        var operatingIdentity = identity ?? _userIdentity;

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            operatingIdentity,
            orchestrationDescription,
            skipStepsBySequence: [3],
            clock: SystemClock.Instance,
            runAt: runAt,
            idempotencyKey: idempotencyKey,
            actorMessageId: new ActorMessageId(Guid.NewGuid().ToString()),
            transactionId: new TransactionId(Guid.NewGuid().ToString()),
            meteringPointId: new MeteringPointId(Guid.NewGuid().ToString()));

        orchestrationInstance.CustomState.SetFromInstance(new TestOrchestrationInstanceCustomState
        {
            TestId = Guid.NewGuid(),
            TestString = "Something new",
        });

        orchestrationInstance.ParameterValue.SetFromInstance(new TestOrchestrationParameter
        {
            TestString = "Test string",
            TestInt = testInt ?? 42,
        });

        return orchestrationInstance;
    }

    /// <summary>
    /// Read data in separate database context.
    /// </summary>
    private async Task<OrchestrationDescription> ReadOrchestrationDescriptionAsync(OrchestrationDescriptionId id)
    {
        OrchestrationDescription orchestrationDescription;

        await using (var dbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            orchestrationDescription = await dbContext.OrchestrationDescriptions.FindAsync(id)
                ?? throw new Exception("Test must arrange data in database before calling this method.");
        }

        return orchestrationDescription;
    }

    /// <summary>
    /// Update data in separate database context.
    /// </summary>
    private async Task UpdateOrchestrationDescriptionAsync(OrchestrationDescription orchestrationDescription)
    {
        await using (var dbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            dbContext.OrchestrationDescriptions.Update(orchestrationDescription);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Read data in separate database context.
    /// </summary>
    private async Task<OrchestrationInstance> ReadOrchestrationInstanceAsync(OrchestrationInstanceId id)
    {
        OrchestrationInstance orchestrationInstance;

        await using (var dbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            orchestrationInstance = await dbContext.OrchestrationInstances.FindAsync(id)
                ?? throw new Exception("Test must arrange data in database before calling this method.");
        }

        return orchestrationInstance;
    }

    /// <summary>
    /// Update data in separate database context.
    /// </summary>
    private async Task UpdateOrchestrationInstanceAsync(OrchestrationInstance orchestrationInstance)
    {
        await using (var dbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            dbContext.OrchestrationInstances.Update(orchestrationInstance);
            await dbContext.SaveChangesAsync();
        }
    }

    private class TestOrchestrationParameter
    {
        public string? TestString { get; set; }

        public int? TestInt { get; set; }
    }

    private class TestOrchestrationInstanceCustomState
    {
        public Guid TestId { get; set; }

        public string? TestString { get; set; }
    }
}
