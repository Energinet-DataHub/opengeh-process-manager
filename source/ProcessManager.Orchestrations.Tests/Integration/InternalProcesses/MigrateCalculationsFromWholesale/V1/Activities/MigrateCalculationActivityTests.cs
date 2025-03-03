﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;

public class MigrateCalculationActivityTests : IClassFixture<MigrateCalculationActivityFixture>, IAsyncLifetime
{
    private readonly MigrateCalculationActivityFixture _fixture;
    private readonly MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1 _sut;
    private readonly WholesaleContext _wholesaleContext;
    private readonly ProcessManagerContext _processManagerContext;

    public MigrateCalculationActivityTests(MigrateCalculationActivityFixture fixture)
    {
        _fixture = fixture;
        _wholesaleContext = _fixture.WholesaleDatabaseManager.CreateDbContext();
        _processManagerContext = _fixture.PMDatabaseManager.CreateDbContext();
        _sut = new MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1(
            _wholesaleContext,
            _processManagerContext,
            new OrchestrationInstanceFactory());
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _wholesaleContext.DisposeAsync();
        await _processManagerContext.DisposeAsync();
    }

    /// <summary>
    /// Testing an "Internal Calculation" which should "skip" the step "Enqueue Messages".
    /// </summary>
    [Fact]
    public async Task Given_InternalCalculationInDatabase_When_CallingActivity_Then_CalculationIsMigrated()
    {
        // Arrange
        var existingCalculationId = Guid.Parse("5dd7bbb3-07f7-4cbd-a74f-17aaed795fa9");

        // Act
        var actual = await _sut.Run(new MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.ActivityInput(existingCalculationId));

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().NotBeNull().And.Contain("Migration succeeded");

        using var wholesaleContext = _fixture.WholesaleDatabaseManager.CreateDbContext();
        using var processManagerContext = _fixture.PMDatabaseManager.CreateDbContext();

        var wholesaleCalculation = await wholesaleContext.Calculations.FindAsync(existingCalculationId);
        var migratedOrchestrationInstance = await processManagerContext.OrchestrationInstances
            .Where(x => x.CustomState == MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.GetCustomState(existingCalculationId))
            .SingleAsync();

        // => Input
        var input = migratedOrchestrationInstance.ParameterValue.AsType<CalculationInputV1>();
        input.CalculationType.Should().Be(CalculationType.Aggregation);
        input.GridAreaCodes.Should().BeEquivalentTo(wholesaleCalculation!.GridAreaCodes.Select(x => x.Code).ToList());
        input.PeriodStartDate.Should().Be(wholesaleCalculation.PeriodStart.ToDateTimeOffset());
        input.PeriodEndDate.Should().Be(wholesaleCalculation.PeriodEnd.ToDateTimeOffset());
        input.IsInternalCalculation.Should().Be(wholesaleCalculation.IsInternalCalculation);

        // => Lifecycle
        migratedOrchestrationInstance.Lifecycle.CreatedBy.Value.As<UserIdentity>().UserId.Value.Should().Be(wholesaleCalculation.CreatedByUserId);
        migratedOrchestrationInstance.Lifecycle.ScheduledToRunAt.Should().Be(wholesaleCalculation.ScheduledAt);
        migratedOrchestrationInstance.Lifecycle.CreatedAt.Should().Be(wholesaleCalculation.CreatedTime);
        migratedOrchestrationInstance.Lifecycle.QueuedAt.Should().Be(wholesaleCalculation.CreatedTime);
        migratedOrchestrationInstance.Lifecycle.TerminatedAt.Should().Be(wholesaleCalculation.CompletedTime);
        migratedOrchestrationInstance.Lifecycle.TerminationState.Should().Be(OrchestrationInstanceTerminationState.Succeeded);

        // => Step: Calculation
        var step1 = migratedOrchestrationInstance.Steps.First(x => x.Sequence == 1);
        migratedOrchestrationInstance.Lifecycle.StartedAt.Should().Be(wholesaleCalculation.ExecutionTimeStart);
        step1.Lifecycle.StartedAt.Should().Be(wholesaleCalculation.ExecutionTimeStart);
        step1.Lifecycle.TerminatedAt.Should().Be(wholesaleCalculation.ExecutionTimeEnd);
        step1.Lifecycle.TerminationState.Should().Be(Core.Domain.OrchestrationInstance.OrchestrationStepTerminationState.Succeeded);

        // => Step: Enqueue Messages
        var step2 = migratedOrchestrationInstance.Steps.First(x => x.Sequence == 2);
        step2.Lifecycle.TerminationState.Should().Be(Core.Domain.OrchestrationInstance.OrchestrationStepTerminationState.Skipped);
    }

    /// <summary>
    /// Testing an "External Calculation" which should run the step "Enqueue Messages".
    /// </summary>
    [Fact]
    public async Task Given_CalculationInDatabase_When_CallingActivity_Then_CalculationIsMigrated()
    {
        // Arrange
        var existingCalculationId = Guid.Parse("00feb707-73af-4d9d-9c85-5a22255cd474");

        // Act
        var actual = await _sut.Run(new MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.ActivityInput(existingCalculationId));

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().NotBeNull().And.Contain("Migration succeeded");

        using var wholesaleContext = _fixture.WholesaleDatabaseManager.CreateDbContext();
        using var processManagerContext = _fixture.PMDatabaseManager.CreateDbContext();

        var wholesaleCalculation = await wholesaleContext.Calculations.FindAsync(existingCalculationId);
        var migratedOrchestrationInstance = await processManagerContext.OrchestrationInstances
            .Where(x => x.CustomState == MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.GetCustomState(existingCalculationId))
            .SingleAsync();

        // => Input
        var input = migratedOrchestrationInstance.ParameterValue.AsType<CalculationInputV1>();
        input.CalculationType.Should().Be(CalculationType.BalanceFixing);
        input.GridAreaCodes.Should().BeEquivalentTo(wholesaleCalculation!.GridAreaCodes.Select(x => x.Code).ToList());
        input.PeriodStartDate.Should().Be(wholesaleCalculation.PeriodStart.ToDateTimeOffset());
        input.PeriodEndDate.Should().Be(wholesaleCalculation.PeriodEnd.ToDateTimeOffset());
        input.IsInternalCalculation.Should().Be(wholesaleCalculation.IsInternalCalculation);

        // => Lifecycle
        migratedOrchestrationInstance.Lifecycle.CreatedBy.Value.As<UserIdentity>().UserId.Value.Should().Be(wholesaleCalculation.CreatedByUserId);
        migratedOrchestrationInstance.Lifecycle.ScheduledToRunAt.Should().Be(wholesaleCalculation.ScheduledAt);
        migratedOrchestrationInstance.Lifecycle.CreatedAt.Should().Be(wholesaleCalculation.CreatedTime);
        migratedOrchestrationInstance.Lifecycle.QueuedAt.Should().Be(wholesaleCalculation.CreatedTime);
        migratedOrchestrationInstance.Lifecycle.TerminatedAt.Should().Be(wholesaleCalculation.CompletedTime);
        migratedOrchestrationInstance.Lifecycle.TerminationState.Should().Be(OrchestrationInstanceTerminationState.Succeeded);

        // => Step: Calculation
        var step1 = migratedOrchestrationInstance.Steps.First(x => x.Sequence == 1);
        migratedOrchestrationInstance.Lifecycle.StartedAt.Should().Be(wholesaleCalculation.ExecutionTimeStart);
        step1.Lifecycle.StartedAt.Should().Be(wholesaleCalculation.ExecutionTimeStart);
        step1.Lifecycle.TerminatedAt.Should().Be(wholesaleCalculation.ExecutionTimeEnd);
        step1.Lifecycle.TerminationState.Should().Be(Core.Domain.OrchestrationInstance.OrchestrationStepTerminationState.Succeeded);

        // => Step: Enqueue Messages
        var step2 = migratedOrchestrationInstance.Steps.First(x => x.Sequence == 2);
        step2.Lifecycle.StartedAt.Should().Be(wholesaleCalculation.ActorMessagesEnqueuingTimeStart);
        step2.Lifecycle.TerminatedAt.Should().Be(wholesaleCalculation.ActorMessagesEnqueuedTimeEnd);
        step2.Lifecycle.TerminationState.Should().Be(Core.Domain.OrchestrationInstance.OrchestrationStepTerminationState.Succeeded);
    }
}
