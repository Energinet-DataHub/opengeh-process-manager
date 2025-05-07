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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using static Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.DomainTestDataFactory;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Database;

public class ProcessManagerReaderContextTests : IClassFixture<ProcessManagerCoreFixture>
{
    private readonly ProcessManagerCoreFixture _fixture;

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
        var existingOrchestrationInstance = CreateActorInitiatedOrchestrationInstance(existingOrchestrationDescription);

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
    /// Demo how the DbSet.FromSql method can be used on OrchestrationInstances, and proves it returns all data similar to a traditional Where query.
    /// </summary>
    [Fact]
    public async Task Given_OrchestrationInstanceWithParametersExistsInDatabase_When_CompareSearchUsingFromSqlAndTraditionalWhereQuery_Then_BothReturnsEquivalentOrchestrationInstance()
    {
        // Arrange
        var expectedTestInt = 55;
        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance = CreateUserInitiatedOrchestrationInstance(
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
        var existingOrchestrationInstance = CreateUserInitiatedOrchestrationInstance(
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
                    AND CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.TestInt') AS int) = {expectedTestInt}
            """);
        // => Show query for easy debugging
        var queryString = query.ToQueryString();
        var actualOrchestrationInstances = await query.ToListAsync();

        // Assert
        actualOrchestrationInstances.Should().Contain(x => x.Id == existingOrchestrationInstance.Id);
        actualOrchestrationInstances.Count.Should().Be(1);
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
