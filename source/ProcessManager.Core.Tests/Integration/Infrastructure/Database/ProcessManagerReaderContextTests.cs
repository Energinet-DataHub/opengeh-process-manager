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
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Database;

public class ProcessManagerReaderContextTests : IClassFixture<ProcessManagerDatabaseFixture>
{
    private readonly ProcessManagerDatabaseFixture _fixture;

    public ProcessManagerReaderContextTests(ProcessManagerDatabaseFixture fixture)
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

    /// <summary>
    /// Demo how the Database.SqlQuery method can be used to retrieve data spanning tables, using WITH and returning a type (not just a scalar).
    /// </summary>
    [Fact]
    public async Task Given_OrchestrationInstanceWithParametersExistsInDatabase_When_UsingSqlQueryToSearchInJsonColumnAndCastResultToFlatOrchestrationInstanceDto_Then_ExpectedOrchestrationInstanceAreReturned()
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

        // Act
        await using var readerContext = _fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();

        IReadOnlyCollection<string> orchestrationDescriptionNames = [
            existingOrchestrationDescription.UniqueName.Name];

        var actualFlatResults = await readerContext.Database
            .SqlQuery<OrchestrationInstanceCustomQueryRow>($"""
                WITH CommonFilter_CTE (
                    OrchestrationDescription_Name,
                    OrchestrationDescription_Version,

                    [Id],
                    [ParameterValue],
                    [CustomState],
                    [IdempotencyKey],
                    [ActorMessageId],
                    [TransactionId],
                    [MeteringPointId],

                    [Lifecycle_CreatedAt],
                    [Lifecycle_QueuedAt],
                    [Lifecycle_ScheduledToRunAt],
                    [Lifecycle_StartedAt],
                    [Lifecycle_State],
                    [Lifecycle_TerminatedAt],
                    [Lifecycle_TerminationState],

                    [Lifecycle_CreatedBy_IdentityType],
                    [Lifecycle_CreatedBy_ActorNumber],
                    [Lifecycle_CreatedBy_ActorRole],
                    [Lifecycle_CreatedBy_UserId],

                    [Lifecycle_CanceledBy_IdentityType],
                    [Lifecycle_CanceledBy_ActorNumber],
                    [Lifecycle_CanceledBy_ActorRole],
                    [Lifecycle_CanceledBy_UserId],

                    Step_Description,
                    Step_Sequence,
                    Step_CustomState,

                    Step_Lifecycle_CanBeSkipped,
                    Step_Lifecycle_StartedAt,
                    Step_Lifecycle_State,
                    Step_Lifecycle_TerminatedAt,
                    Step_Lifecycle_TerminationState
                )
                AS (
                    SELECT
                        [od].[Name] as OrchestrationDescription_Name,
                        [od].[Version] as OrchestrationDescription_Version,

                        [oi].[Id],
                        [oi].[ParameterValue],
                        [oi].[CustomState],
                        [oi].[IdempotencyKey],
                        [oi].[ActorMessageId],
                        [oi].[TransactionId],
                        [oi].[MeteringPointId],

                        [oi].[Lifecycle_CreatedAt],
                        [oi].[Lifecycle_QueuedAt],
                        [oi].[Lifecycle_ScheduledToRunAt],
                        [oi].[Lifecycle_StartedAt],
                        [oi].[Lifecycle_State],
                        [oi].[Lifecycle_TerminatedAt],
                        [oi].[Lifecycle_TerminationState],

                        [oi].[Lifecycle_CreatedBy_IdentityType],
                        [oi].[Lifecycle_CreatedBy_ActorNumber],
                        [oi].[Lifecycle_CreatedBy_ActorRole],
                        [oi].[Lifecycle_CreatedBy_UserId],

                        [oi].[Lifecycle_CanceledBy_IdentityType],
                        [oi].[Lifecycle_CanceledBy_ActorNumber],
                        [oi].[Lifecycle_CanceledBy_ActorRole],
                        [oi].[Lifecycle_CanceledBy_UserId],

                        [si].[Description] as Step_Description,
                        [si].[Sequence] as Step_Sequence,
                        [si].[CustomState] as Step_CustomState,

                        [si].[Lifecycle_CanBeSkipped] as Step_Lifecycle_CanBeSkipped,
                        [si].[Lifecycle_StartedAt] as Step_Lifecycle_StartedAt,
                        [si].[Lifecycle_State] as Step_Lifecycle_State,
                        [si].[Lifecycle_TerminatedAt] as Step_Lifecycle_TerminatedAt,
                        [si].[Lifecycle_TerminationState] as Step_Lifecycle_TerminationState
                    FROM
                        [pm].[OrchestrationDescription] AS [od]
                    INNER JOIN
                        [pm].[OrchestrationInstance] AS [oi] ON [od].[Id] = [oi].[OrchestrationDescriptionId]
                    LEFT JOIN
                        [pm].[StepInstance] AS [si] ON [oi].[Id] = [si].[OrchestrationInstanceId]
                    WHERE
                        [od].[Name] IN (
                            SELECT [names].[value]
                            FROM OPENJSON({orchestrationDescriptionNames}) WITH ([value] nvarchar(max) '$') AS [names]
                        )
                )

                SELECT
                    *
                FROM
                    CommonFilter_CTE as [cf]
                WHERE
                    CAST(JSON_VALUE([cf].[ParameterValue],'$.TestInt') AS int) = {expectedTestInt}
                """)
            .ToListAsync();
        ////var actualObjectResult = actualFlatResult
        ////  .GroupBy(x => new
        ////  {
        ////      x.Id,

        ////      x.Lifecycle_CreatedBy,
        ////      x.Lifecycle_State,
        ////      x.Lifecycle_TerminationState,
        ////      x.Lifecycle_CanceledBy,
        ////      x.Lifecycle_CreatedAt,
        ////      x.Lifecycle_ScheduledToRunAt,
        ////      x.Lifecycle_QueuedAt,
        ////      x.Lifecycle_StartedAt,
        ////      x.Lifecycle_TerminatedAt,

        ////      x.ParameterValue,
        ////      x.CustomState,
        ////      x.IdempotencyKey,
        ////      x.ActorMessageId,
        ////      x.TransactionId,
        ////      x.MeteringPointId,

        ////      // List all but Steps...
        ////  })
        ////  .Select(x => new OrchestrationInstance()
        ////  {
        ////      id = x.Key.Id,
        ////      desc = x,
        ////      Key.ProductDesc,
        ////      .... // etc.
        ////      images = x.Select(i => i.ProdImgThumb).ToList()
        ////  });

        // Assert
        actualFlatResults.Should().Contain(x => x.Id == existingOrchestrationInstance.Id.Value);
        actualFlatResults.Count.Should().Be(3); // TODO: Update, but for now it's one per step - must group and create instances
    }

    /// <summary>
    /// Demo how the DbSet.FromSql method can be used on OrchestrationInstances, and proves it returns all data similar to a traditional Where query.
    /// </summary>
    [Fact]
    public async Task Given_OrchestrationInstanceWithParametersExistsInDatabase_When_CompareSearchUsingFromSqlAndTraditionalWhereQuery_Then_BothReturnsEquivalentOrchestrationInstance()
    {
        // Arrange
        var expectedTestInt = 55;
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
        // => Demo how we can "fix" issue where FromSql incorrectly think the tables names are "CustomState_SerializedValue" and "ParameterValue_SerializedValue".
        var result01 = readerContext.OrchestrationInstances
            .FromSql($"""
                SELECT
                    [oi].[Id],
                    [oi].[ActorMessageId],
                    [oi].[IdempotencyKey],
                    [oi].[MeteringPointId],
                    [oi].[OrchestrationDescriptionId],
                    [oi].[RowVersion],
                    [oi].[TransactionId],
                    [oi].[CustomState] as CustomState_SerializedValue,
                    [oi].[ParameterValue] as ParameterValue_SerializedValue,
                    [oi].[Lifecycle_CreatedAt],
                    [oi].[Lifecycle_QueuedAt],
                    [oi].[Lifecycle_ScheduledToRunAt],
                    [oi].[Lifecycle_StartedAt],
                    [oi].[Lifecycle_State],
                    [oi].[Lifecycle_TerminatedAt],
                    [oi].[Lifecycle_TerminationState],
                    [oi].[Lifecycle_CanceledBy_ActorNumber],
                    [oi].[Lifecycle_CanceledBy_ActorRole],
                    [oi].[Lifecycle_CanceledBy_IdentityType],
                    [oi].[Lifecycle_CanceledBy_UserId],
                    [oi].[Lifecycle_CreatedBy_ActorNumber],
                    [oi].[Lifecycle_CreatedBy_ActorRole],
                    [oi].[Lifecycle_CreatedBy_IdentityType],
                    [oi].[Lifecycle_CreatedBy_UserId]
                FROM
                    [pm].OrchestrationInstance as [oi]
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

    [Fact]
    public async Task Given_OrchestrationInstanceWithParametersExistsInDatabase_When_UsingFromSqlToSearchInJsonColumn_Then_ExpectedOrchestrationInstanceAreReturned()
    {
        // Arrange
        var expectedTestInt = 56;
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

        IReadOnlyCollection<string> orchestrationDescriptionNames = [
            existingOrchestrationDescription.UniqueName.Name];

        // => Demo how we can "fix" issue where FromSql incorrectly think the tables names are "CustomState_SerializedValue" and "ParameterValue_SerializedValue".
        var query = readerContext.OrchestrationInstances
            .FromSql($"""
                SELECT
                    [oi].[Id],
                    [oi].[ActorMessageId],
                    [oi].[IdempotencyKey],
                    [oi].[MeteringPointId],
                    [oi].[OrchestrationDescriptionId],
                    [oi].[RowVersion],
                    [oi].[TransactionId],
                    [oi].[CustomState] as CustomState_SerializedValue,
                    [oi].[ParameterValue] as ParameterValue_SerializedValue,
                    [oi].[Lifecycle_CreatedAt],
                    [oi].[Lifecycle_QueuedAt],
                    [oi].[Lifecycle_ScheduledToRunAt],
                    [oi].[Lifecycle_StartedAt],
                    [oi].[Lifecycle_State],
                    [oi].[Lifecycle_TerminatedAt],
                    [oi].[Lifecycle_TerminationState],
                    [oi].[Lifecycle_CanceledBy_ActorNumber],
                    [oi].[Lifecycle_CanceledBy_ActorRole],
                    [oi].[Lifecycle_CanceledBy_IdentityType],
                    [oi].[Lifecycle_CanceledBy_UserId],
                    [oi].[Lifecycle_CreatedBy_ActorNumber],
                    [oi].[Lifecycle_CreatedBy_ActorRole],
                    [oi].[Lifecycle_CreatedBy_IdentityType],
                    [oi].[Lifecycle_CreatedBy_UserId]
                FROM
                    [pm].[OrchestrationDescription] AS [od]
                INNER JOIN
                    [pm].[OrchestrationInstance] AS [oi] ON [od].[Id] = [oi].[OrchestrationDescriptionId]
                WHERE
                    [od].[Name] IN (
                        SELECT [names].[value]
                        FROM OPENJSON({orchestrationDescriptionNames}) WITH ([value] nvarchar(max) '$') AS [names]
                    )
                    AND CAST(JSON_VALUE([oi].[ParameterValue],'$.TestInt') AS int) = {expectedTestInt}
            """);
        // => Show query for easy debugging
        var queryString = query.ToQueryString();
        var actualOrchestrationInstances = await query.ToListAsync();

        // Assert
        actualOrchestrationInstances.Should().Contain(x => x.Id == existingOrchestrationInstance.Id);
        actualOrchestrationInstances.Count.Should().Be(1);
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
        Instant? runAt = default,
        int? testInt = default,
        IdempotencyKey? idempotencyKey = default)
    {
        var operatingIdentity = ProcessManagerDomainTestDataFactory.EnergySupplier.UserIdentity;

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

    /// <summary>
    /// Can be used with EF Core method "SqlQuery" to retrieve Orchestration Instances
    /// using raw SQL statements.
    /// </summary>
    public record OrchestrationInstanceCustomQueryRow(
            string OrchestrationDescription_Name,
            int OrchestrationDescription_Version,

            Guid Id,
            string ParameterValue,
            string CustomState,
            string? IdempotencyKey,
            string? ActorMessageId,
            string? TransactionId,
            string? MeteringPointId,

            Instant Lifecycle_CreatedAt,
            Instant? Lifecycle_QueuedAt,
            Instant? Lifecycle_ScheduledToRunAt,
            Instant? Lifecycle_StartedAt,
            OrchestrationInstanceLifecycleState Lifecycle_State,
            Instant? Lifecycle_TerminatedAt,
            OrchestrationInstanceTerminationState? Lifecycle_TerminationState,

            string Lifecycle_CreatedBy_IdentityType,
            string? Lifecycle_CreatedBy_ActorNumber,
            string? Lifecycle_CreatedBy_ActorRole,
            Guid? Lifecycle_CreatedBy_UserId,

            string? Lifecycle_CanceledBy_IdentityType,
            string? Lifecycle_CanceledBy_ActorNumber,
            string? Lifecycle_CanceledBy_ActorRole,
            Guid? Lifecycle_CanceledBy_UserId,

            string Step_Description,
            int Step_Sequence,
            string Step_CustomState,

            bool Step_Lifecycle_CanBeSkipped,
            Instant? Step_Lifecycle_StartedAt,
            StepInstanceLifecycleState Step_Lifecycle_State,
            Instant? Step_Lifecycle_TerminatedAt,
            StepInstanceTerminationState? Step_Lifecycle_TerminationState);
}
