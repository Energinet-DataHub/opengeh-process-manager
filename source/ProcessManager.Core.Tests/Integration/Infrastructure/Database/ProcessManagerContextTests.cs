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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Newtonsoft.Json;
using NodaTime;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Database;

public class ProcessManagerContextTests : IClassFixture<ProcessManagerCoreFixture>
{
    private readonly ProcessManagerCoreFixture _fixture;

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
    public async Task Given_OrchestrationInstanceWithStepsAddedToDbContext_When_FilteringJsonColumn_Then_ReturnsExpectedItem()
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
    public async Task Given_UserCanceledOrchestrationInstanceAddedToDbContext_When_RetrievingFromDatabase_Then_HasCorrectValues()
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

    [Fact]
    public async Task Given_OrchestrationDescriptionChangedFromMultipleConsumer_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
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
    public async Task Given_OrchestrationInstanceChangedFromMultipleConsumer_When_SavingChanges_Then_OptimisticConcurrencyEnsureExceptionIsThrown()
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
        orchestrationInstanceFromContext01.TransitionStepToRunning(1, SystemClock.Instance);

        var orchestrationInstanceFromContext02 = await ReadOrchestrationInstanceAsync(existingOrchestrationInstance.Id);
        orchestrationInstanceFromContext02.TransitionStepToRunning(2, SystemClock.Instance);

        await UpdateOrchestrationInstanceAsync(orchestrationInstanceFromContext01);

        // Act
        var act = () => UpdateOrchestrationInstanceAsync(orchestrationInstanceFromContext02);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Given_OrchestrationInstanceWithStepsAddedToDbContext_When_FilteringJsonColumnUsingLINQ_Then_ReturnsExpectedItem()
    {
        // Arrange
        var expectedPeriodStart1 = DateTime.Now;
        var expectedPeriodEnd1 = DateTime.Now.AddDays(1);
        var expectedIsInternalCalculation1 = true;

        var expectedPeriodStart2 = DateTime.Now.AddDays(2);
        var expectedPeriodEnd2 = DateTime.Now.AddDays(3);
        var expectedIsInternalCalculation2 = false;

        var testParameter1 = new TestOrchestrationParameter
        {
            PeriodStartDate = expectedPeriodStart1,
            PeriodEndDate = expectedPeriodEnd1,
            IsInternalCalculation = expectedIsInternalCalculation1,
            CalculationTypes = new List<string?> { "0", "1" },
            GridAreaCodes = new List<string?> { "804" },
        };

        var testParameter2 = new TestOrchestrationParameter
        {
            PeriodStartDate = expectedPeriodStart2,
            PeriodEndDate = expectedPeriodEnd2,
            IsInternalCalculation = expectedIsInternalCalculation2,
            CalculationTypes = new List<string?> { "2" },
            GridAreaCodes = new List<string?> { "708", "804" },
        };

        var searchParams = new TestOrchestrationParameter
        {
            PeriodStartDate = DateTime.Now,
            PeriodEndDate = DateTime.Now.AddDays(3),
            IsInternalCalculation = false,
            CalculationTypes = new List<string?> { "2" },
            GridAreaCodes = new List<string?> { "708" },
        };

        var existingOrchestrationDescription = CreateOrchestrationDescription();
        var existingOrchestrationInstance1 = CreateOrchestrationInstance(existingOrchestrationDescription, testParameter: testParameter1);
        var existingOrchestrationInstance2 = CreateOrchestrationInstance(existingOrchestrationDescription, testParameter: testParameter2);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance1);
            writeDbContext.OrchestrationInstances.Add(existingOrchestrationInstance2);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var orchestrationInstanceList = readDbContext.OrchestrationInstances.ToList();

        var calculationsToFilter = orchestrationInstanceList
            .Select(x => new
            {
                OrchestrationId = x.Id,
                ParameterValue = JsonConvert.DeserializeObject<TestOrchestrationParameter>(x.ParameterValue.SerializedParameterValue),
            });

        var calculationTypesSet = searchParams.CalculationTypes?.ToHashSet();
        var gridAreaCodesSet = searchParams.GridAreaCodes?.ToHashSet();

        var filteredCalculations = calculationsToFilter
            .Where(calculation =>
                (calculationTypesSet == null || calculation.ParameterValue?.CalculationTypes?.Any(calculationTypesSet.Contains) != false) &&
                (gridAreaCodesSet == null || calculation.ParameterValue?.GridAreaCodes?.Any(gridAreaCodesSet.Contains) != false) &&
                (searchParams.PeriodStartDate == null || calculation.ParameterValue?.PeriodStartDate >= searchParams.PeriodStartDate) &&
                (searchParams.PeriodEndDate == null || calculation.ParameterValue?.PeriodEndDate <= searchParams.PeriodEndDate) &&
                (searchParams.IsInternalCalculation == null || calculation.ParameterValue?.IsInternalCalculation == searchParams.IsInternalCalculation))
            .Select(calculation => calculation.OrchestrationId)
            .ToHashSet();

        // Assert
        filteredCalculations.Count.Should().Be(1);
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
        TestOrchestrationParameter? testParameter = default,
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

        if (testParameter != null)
        {
            orchestrationInstance.ParameterValue.SetFromInstance(testParameter);
        }

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
        public List<string?>? CalculationTypes { get; set; }

        public List<string?>? GridAreaCodes { get; set; }

        public DateTimeOffset? PeriodStartDate { get; set; }

        public DateTimeOffset? PeriodEndDate { get; set; }

        public bool? IsInternalCalculation { get; set; }
    }
}
