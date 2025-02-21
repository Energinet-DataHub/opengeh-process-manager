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

using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using CalculationType = Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale.Model.CalculationType;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;

public class ValidateMigratedCalculationsActivityTests : IClassFixture<MigrateCalculationActivityFixture>, IAsyncLifetime
{
    private readonly MigrateCalculationActivityFixture _fixture;
    private readonly WholesaleContext _wholesaleContext;
    private readonly ProcessManagerContext _processManagerContext;
    private readonly ValidateMigratedCalculationsActivity_MigrateCalculationsFromWholesale_V1 _sut;

    public ValidateMigratedCalculationsActivityTests(MigrateCalculationActivityFixture fixture)
    {
        _fixture = fixture;

        _wholesaleContext = _fixture.WholesaleDatabaseManager.CreateDbContext();
        _processManagerContext = _fixture.PMDatabaseManager.CreateDbContext();
        _sut = new ValidateMigratedCalculationsActivity_MigrateCalculationsFromWholesale_V1(
            Mock.Of<ILogger<ValidateMigratedCalculationsActivity_MigrateCalculationsFromWholesale_V1>>(),
            _wholesaleContext,
            _processManagerContext);
    }

    public async Task InitializeAsync()
    {
        // Clear all Wholesale calculations, the tests create their own.
        await _wholesaleContext.Calculations.ExecuteDeleteAsync();

        // Clear all migrated calculations.
        await _processManagerContext.Database.ExecuteSqlAsync($"DELETE FROM [pm].[StepInstance]");
        await _processManagerContext.OrchestrationInstances.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await _wholesaleContext.DisposeAsync();
        await _processManagerContext.DisposeAsync();
    }

    [Fact]
    public async Task Given_AllCalculationsMigratedCorrectly_When_CallingActivity_ReturnsMigrationSucceeded()
    {
        // Given
        // => Create wholesale calculation & migrate it (correctly)
        await using (var wholesaleContext = _fixture.WholesaleDatabaseManager.CreateDbContext())
        {
            await using var processManagerContext = _fixture.PMDatabaseManager.CreateDbContext();
            // Create wholesale calculation
            var calculation = CreateWholesaleCalculation();
            wholesaleContext.Add(calculation);
            await wholesaleContext.SaveChangesAsync();

            // Migrate calculation
            await new MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1(
                    wholesaleContext,
                    processManagerContext,
                    new OrchestrationInstanceFactory())
                .Run(new MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.ActivityInput(
                    CalculationToMigrateId: calculation.Id));
        }

        // When
        var result = await _sut.Run(null!);

        // Then
        result.Should().Be($"Successfully validated 1 migrated calculations.");
    }

    [Fact]
    public async Task Given_OneCalculationNotMigrated_When_CallingActivity_ThrowsExceptionWithNotMigratedMessage()
    {
        // Given
        // => Create wholesale calculation & migrate it incorrectly
        Guid wholesaleCalculationId;
        await using (var wholesaleContext = _fixture.WholesaleDatabaseManager.CreateDbContext())
        {
            await using var processManagerContext = _fixture.PMDatabaseManager.CreateDbContext();
            // Create wholesale calculation
            var calculation = CreateWholesaleCalculation();
            wholesaleContext.Add(calculation);
            await wholesaleContext.SaveChangesAsync();

            wholesaleCalculationId = calculation.Id;
        }

        // When
        var act = () => _sut.Run(null!);

        // Then
        (await act.Should().ThrowAsync<Exception>())
            .WithMessage($"*Errors while migrating Wholesale calculations.*{wholesaleCalculationId}: [Not migrated]*");
    }

    [Fact]
    public async Task Given_OneCalculationMigratedIncorrectly_When_CallingActivity_ThrowsExceptionWithErrorsInMessage()
    {
        // Given
        // => Create wholesale calculation & migrate it incorrectly
        Guid wholesaleCalculationId;
        await using (var wholesaleContext = _fixture.WholesaleDatabaseManager.CreateDbContext())
        {
            await using var processManagerContext = _fixture.PMDatabaseManager.CreateDbContext();
            // Create wholesale calculation
            var calculation = CreateWholesaleCalculation();
            wholesaleContext.Add(calculation);
            await wholesaleContext.SaveChangesAsync();

            // Migrate calculation
            var orchestrationDescription = await processManagerContext.OrchestrationDescriptions.SingleAsync();
            var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
                identity: DataHubSystemAdministrator.UserIdentity,
                description: orchestrationDescription,
                skipStepsBySequence: [],
                clock: SystemClock.Instance);

            orchestrationInstance.ParameterValue.SetFromInstance(new CalculationInputV1(
                CalculationType: (Abstractions.Processes.BRS_023_027.V1.Model.CalculationType)int.MinValue,
                GridAreaCodes: [],
                PeriodStartDate: default,
                PeriodEndDate: default,
                IsInternalCalculation: false));

            orchestrationInstance.CustomState.Value = MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1
                .GetMigratedWholesaleCalculationIdCustomState(calculation.Id)
                .Value;

            processManagerContext.OrchestrationInstances.Add(orchestrationInstance);
            await processManagerContext.SaveChangesAsync();

            wholesaleCalculationId = calculation.Id;
        }

        // When
        var act = () => _sut.Run(null!);

        // Then
        (await act.Should().ThrowAsync<Exception>())
            .WithMessage($"*Errors while migrating Wholesale calculations.*")
            .WithMessage($"*{wholesaleCalculationId}: [*TerminationState*]*")
            .WithMessage($"*{wholesaleCalculationId}: [*CalculationType*]*")
            .WithMessage($"*{wholesaleCalculationId}: [*GridAreaCodes*]*")
            .WithMessage($"*{wholesaleCalculationId}: [*PeriodStartDate*]*")
            .WithMessage($"*{wholesaleCalculationId}: [*PeriodEndDate*]*")
            .WithMessage($"*{wholesaleCalculationId}: [*Step 1: TerminationState*]*")
            .WithMessage($"*{wholesaleCalculationId}: [*Step 2: TerminationState*]*");
    }

    private Calculation CreateWholesaleCalculation()
    {
        var createdTime = Instant.FromUtc(2025, 02, 21, 13, 37);
        var scheduledAt = createdTime.Plus(Duration.FromSeconds(5));
        var wholesaleCalculation = new Calculation(
            createdTime: createdTime,
            calculationType: CalculationType.WholesaleFixing,
            gridAreaCodes: [new GridAreaCode("111"), new GridAreaCode("222")],
            periodStart: Instant.FromUtc(2025, 1, 1, 0, 0),
            periodEnd: Instant.FromUtc(2025, 2, 1, 0, 0),
            scheduledAt: scheduledAt,
            dateTimeZone: DateTimeZone.Utc,
            createdByUserId: Guid.NewGuid(),
            version: 1,
            isInternalCalculation: false);

        wholesaleCalculation.MarkAsStarted();
        var executionTimeStart = scheduledAt.Plus(Duration.FromSeconds(45));
        wholesaleCalculation.MarkAsCalculationJobSubmitted(new CalculationJobId(1), executionTimeStart);

        var executionTimeEnd = executionTimeStart.Plus(Duration.FromMinutes(12));
        wholesaleCalculation.MarkAsCalculated(executionTimeEnd);

        var enqueuingTimeStart = executionTimeEnd.Plus(Duration.FromSeconds(23));
        wholesaleCalculation.MarkAsActorMessagesEnqueuing(enqueuingTimeStart);
        var enqueuedTimeEnd = enqueuingTimeStart.Plus(Duration.FromMinutes(4));
        wholesaleCalculation.MarkAsActorMessagesEnqueued(enqueuedTimeEnd);
        wholesaleCalculation.MarkAsCompleted(enqueuedTimeEnd.Plus(Duration.FromMilliseconds(41)));

        return wholesaleCalculation;
    }
}
