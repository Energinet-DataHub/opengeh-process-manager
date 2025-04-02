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
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Database;

public class ProcessManagerReaderContextTests : IClassFixture<ProcessManagerCoreFixture>
{
    private readonly ProcessManagerCoreFixture _fixture;
    private readonly UserIdentity _userIdentity = new(
        new UserId(Guid.NewGuid()),
        new Actor(ActorNumber.Create("1234567890123"), ActorRole.EnergySupplier));

    public ProcessManagerReaderContextTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionAddedToDbContext_When_SaveChangesAsync_Then_ThrowsExpectedException()
    {
        // Arrange
        var newOrchestrationDescription = CreateOrchestrationDescription();

        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        readerContext.OrchestrationDescriptions.Add(newOrchestrationDescription);

        // Act
        var act = () => readerContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionAddedToDbContext_When_SaveChangesAsyncWithAcceptAllChangesOnSuccess_Then_ThrowsExpectedException()
    {
        // Arrange
        var newOrchestrationDescription = CreateOrchestrationDescription();

        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        readerContext.OrchestrationDescriptions.Add(newOrchestrationDescription);

        // Act
        var act = () => readerContext.SaveChangesAsync(acceptAllChangesOnSuccess: true);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionAddedToDbContext_When_SaveChanges_Then_ThrowsExpectedException()
    {
        // Arrange
        var newOrchestrationDescription = CreateOrchestrationDescription();

        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        readerContext.OrchestrationDescriptions.Add(newOrchestrationDescription);

        // Act
        var act = () => readerContext.SaveChanges();

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionAddedToDbContext_When_SaveChangesWithAcceptAllChangesOnSuccess_Then_ThrowsExpectedException()
    {
        // Arrange
        var newOrchestrationDescription = CreateOrchestrationDescription();

        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        readerContext.OrchestrationDescriptions.Add(newOrchestrationDescription);

        // Act
        var act = () => readerContext.SaveChanges(acceptAllChangesOnSuccess: true);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task Given_OrchestrationDescriptionExistsInDatabase_When_RetrievingFromDatabase_Then_HasCorrectValues()
    {
        // Arrange
        var existingOrchestrationDescription = CreateOrchestrationDescription();

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        var orchestrationDescription = await readerContext.OrchestrationDescriptions.FindAsync(existingOrchestrationDescription.Id);

        // Assert
        orchestrationDescription.Should()
            .NotBeNull()
            .And
            .BeEquivalentTo(existingOrchestrationDescription);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceExistsInDatabase_When_RetrievingFromDatabase_Then_HasCorrectValues()
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
        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        var orchestrationInstance = await readerContext.OrchestrationInstances.FindAsync(existingOrchestrationInstance.Id);

        // Assert
        orchestrationInstance.Should()
            .NotBeNull()
            .And
            .BeEquivalentTo(existingOrchestrationInstance);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceWithParametersExistsInDatabase_When_UsingSqlQueryToSearchInJsonColumnAndReturnId_Then_ExpectedIdIsReturned()
    {
        // Arrange
        var expectedTestInt = 52;
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            testInt: expectedTestInt);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        var actualIds = await readerContext.Database
            .SqlQuery<Guid>($"""
                SELECT
                    [o].[Id]
                FROM
                    [pm].[OrchestrationInstance] AS [o]
                WHERE
                    CAST(JSON_VALUE([o].[ParameterValue],'$.TestInt') AS int) = {expectedTestInt}
                """)
            .ToListAsync();

        // Assert
        actualIds.Should().Contain(existingOrchestrationInstance.Id.Value);
        actualIds.Count.Should().Be(1);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceWithParametersExistsInDatabase_When_UsingSqlQueryToSearchInJsonColumnAndCastResultToParameterType_Then_ExpectedParametersAreReturned()
    {
        // Arrange
        var expectedTestInt = 53;
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            testInt: expectedTestInt);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        var existingParameters = existingOrchestrationInstance.ParameterValue.AsType<TestOrchestrationParameter>();

        // Act
        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        var actualParameters = await readerContext.Database
            .SqlQuery<TestOrchestrationParameter>($"""
                SELECT
                    CAST(JSON_VALUE([o].[ParameterValue],'$.TestString') AS nvarchar) AS TestString,
                    CAST(JSON_VALUE([o].[ParameterValue],'$.TestInt') AS int) AS TestInt
                FROM
                    [pm].[OrchestrationInstance] AS [o]
                WHERE
                    CAST(JSON_VALUE([o].[ParameterValue],'$.TestInt') AS int) = {expectedTestInt}
                """)
            .ToListAsync();

        // Assert
        actualParameters.Should().ContainEquivalentOf(existingParameters);
        actualParameters.Count.Should().Be(1);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceWithParametersExistsInDatabase_When_CompareSearchUsingFromSqlRawAndTraditionalWhereQuery_Then_BothReturnsEquivalentOrchestrationInstance()
    {
        // Arrange
        var expectedTestInt = 54;
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateOrchestrationInstance(
            existingOrchestrationDescription,
            testInt: expectedTestInt);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        var existingParameters = existingOrchestrationInstance.ParameterValue.AsType<TestOrchestrationParameter>();

        // Act
        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        // => Demo how we can "fix" issue where FromSqlRaw incorrectly think the tables names are "CustomState_SerializedValue" and "ParameterValue_SerializedValue".
        var result01 = readerContext.OrchestrationInstances
            .FromSqlRaw("""
                SELECT
                    [o].[Id],
                    [o].[ActorMessageId],
                    [o].[IdempotencyKey],
                    [o].[MeteringPointId],
                    [o].[OrchestrationDescriptionId],
                    [o].[RowVersion],
                    [o].[TransactionId],
                    [o].[CustomState] as CustomState_SerializedValue,
                    [o].[ParameterValue] as ParameterValue_SerializedValue,
                    [o].[Lifecycle_CreatedAt],
                    [o].[Lifecycle_QueuedAt],
                    [o].[Lifecycle_ScheduledToRunAt],
                    [o].[Lifecycle_StartedAt],
                    [o].[Lifecycle_State],
                    [o].[Lifecycle_TerminatedAt],
                    [o].[Lifecycle_TerminationState],
                    [o].[Lifecycle_CanceledBy_ActorNumber],
                    [o].[Lifecycle_CanceledBy_ActorRole],
                    [o].[Lifecycle_CanceledBy_IdentityType],
                    [o].[Lifecycle_CanceledBy_UserId],
                    [o].[Lifecycle_CreatedBy_ActorNumber],
                    [o].[Lifecycle_CreatedBy_ActorRole],
                    [o].[Lifecycle_CreatedBy_IdentityType],
                    [o].[Lifecycle_CreatedBy_UserId]
                FROM
                    [pm].OrchestrationInstance as [o]
            """)
            .Where(x => x.Id == existingOrchestrationInstance.Id);
        // => Show query for easy debugging
        var queryString01 = result01.ToQueryString();
        var result01OrchestrationInstance = await result01.FirstOrDefaultAsync();

        var result02 = readerContext.OrchestrationInstances
            .Where(x => x.Id == existingOrchestrationInstance.Id);
        // => Show query for easy debugging
        var queryString02 = result02.ToQueryString();
        var result02OrchestrationInstance = await result01.FirstOrDefaultAsync();

        // Assert
        result01OrchestrationInstance.Should().BeEquivalentTo(result02OrchestrationInstance);
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
