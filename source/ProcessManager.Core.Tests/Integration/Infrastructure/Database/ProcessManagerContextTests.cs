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
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Database;

public class ProcessManagerContextTests : IClassFixture<ProcessManagerCoreFixture>
{
    private readonly ProcessManagerCoreFixture _fixture;

    public ProcessManagerContextTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionAddedToDbContext_WhenRetrievingFromDatabase_HasCorrectValues()
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
    public async Task Given_RecurringOrchestrationDescriptionAddedToDbContext_WhenRetrievingFromDatabase_HasCorrectValues()
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
    public async Task Given_OrchestrationInstanceWithStepsAddedToDbContext_WhenRetrievingFromDatabase_HasCorrectValues()
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
    public async Task Given_OrchestrationInstanceWithUniqueIdempotencyKeyAddedToDbContext_WhenRetrievingFromDatabase_HasCorrectValues()
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
    public async Task Given_MultipleOrchestrationInstancesWithNullInIdempotencyKeyAddedToDbContext_WhenSaveChangesAsync_NoExceptionThrown()
    {
        // Arrange
        IdempotencyKey? idempotencyKey = null;
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance01 = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: idempotencyKey);
        var existingOrchestrationInstance02 = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: idempotencyKey);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance01);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance02);
            // Act
            await writeDbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Given_MultipleOrchestrationInstancesWithSameValueInIdempotencyKeyAddedToDbContext_WhenSaveChangesAsync_ThrowsExpectedException()
    {
        // Arrange
        var idempotencyKey = new IdempotencyKey(Guid.NewGuid().ToString());
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance01 = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: idempotencyKey);
        var existingOrchestrationInstance02 = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            idempotencyKey: idempotencyKey);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance01);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance02);
            // Act
            var act = () => writeDbContext.SaveChangesAsync();
            // Assert
            var assertionScope = new AssertionScope();
            var ex = await act.Should()
                .ThrowAsync<DbUpdateException>();
            ex.Which!.InnerException!.Message
                .Contains("Cannot insert duplicate key row in object 'pm.OrchestrationInstance' with unique index 'UX_OrchestrationInstance_IdempotencyKey'");
        }
    }

    [Fact]
    public async Task Given_OrchestrationInstanceWithStepsAddedToDbContext_WhenFilteringJsonColumn_ReturnsExpectedItem()
    {
        // Arrange
        var expectedTestInt = 52;
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(existingOrchestrationDescription, testInt: expectedTestInt);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationInstanceIds = await readDbContext.Database
            .SqlQuery<Guid>($"SELECT [o].[Id] FROM [pm].[OrchestrationInstance] AS [o] WHERE CAST(JSON_VALUE([o].[SerializedParameterValue],'$.TestInt') AS int) = {expectedTestInt}")
            .ToListAsync();

        // Assert
        actualOrchestrationInstanceIds.Should().Contain(existingOrchestrationInstance.Id.Value);
        actualOrchestrationInstanceIds.Count.Should().Be(1);
    }

    [Fact]
    public async Task Given_UserCanceledOrchestrationInstanceAddedToDbContext_WhenRetrievingFromDatabase_HasCorrectValues()
    {
        // Arrange
        var userIdentity = new UserIdentity(new UserId(Guid.NewGuid()), new ActorId(Guid.NewGuid()));

        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            identity: userIdentity,
            runAt: SystemClock.Instance.GetCurrentInstant());
        existingOrchestrationInstance.Lifecycle.TransitionToUserCanceled(SystemClock.Instance, userIdentity);

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

    private static OrchestrationInstance CreateOrchestrationInstance(
        OrchestrationDescription orchestrationDescription,
        OperatingIdentity? identity = default,
        Instant? runAt = default,
        int? testInt = default,
        IdempotencyKey? idempotencyKey = default)
    {
        var operatingIdentity = identity
            ?? new UserIdentity(
                new UserId(Guid.NewGuid()),
                new ActorId(Guid.NewGuid()));

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            operatingIdentity,
            orchestrationDescription,
            skipStepsBySequence: [3],
            clock: SystemClock.Instance,
            runAt,
            idempotencyKey);

        orchestrationInstance.ParameterValue.SetFromInstance(new TestOrchestrationParameter
        {
            TestString = "Test string",
            TestInt = testInt ?? 42,
        });

        return orchestrationInstance;
    }

    private class TestOrchestrationParameter
    {
        public string? TestString { get; set; }

        public int? TestInt { get; set; }
    }
}
