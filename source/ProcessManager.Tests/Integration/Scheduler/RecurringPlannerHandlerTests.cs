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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Scheduling;
using Energinet.DataHub.ProcessManager.Scheduler;
using Energinet.DataHub.ProcessManager.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using static Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.DomainTestDataFactory;

namespace Energinet.DataHub.ProcessManager.Tests.Integration.Scheduler;

public class RecurringPlannerHandlerTests : IClassFixture<RecurringPlannerHandlerFixture>, IAsyncLifetime
{
    private readonly RecurringPlannerHandlerFixture _fixture;
    private readonly Mock<IStartOrchestrationInstanceCommands> _managerMock;
    private readonly ProcessManagerContext _dbContext;
    private readonly RecurringPlannerHandler _sut;

    public RecurringPlannerHandlerTests(RecurringPlannerHandlerFixture fixture)
    {
        _fixture = fixture;

        _managerMock = new Mock<IStartOrchestrationInstanceCommands>();

        _dbContext = _fixture.DatabaseManager.CreateDbContext();
        var queries = new RecurringOrchestrationQueries(_dbContext);

        _sut = new RecurringPlannerHandler(
            Mock.Of<ILogger<RecurringPlannerHandler>>(),
            _fixture.DkTimeZone,
            _fixture.ClockMock.Object,
            queries,
            _managerMock.Object);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _fixture.DatabaseManager.ExecuteDeleteOnEntitiesAsync();
    }

    [Fact]
    public async Task GivenRecurringOrchestrationDescriptionPlannedFor12and17_WhenNoOrchestrationInstancesAreScheduled_ThenTwoNewOrchestrationInstancesAreScheduled()
    {
        // Arrange
        var uniqueName = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var orchestrationDescription = CreateOrchestrationDescription(
            uniqueName,
            recurringCronExpression: "0 12,17 * * *");

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(orchestrationDescription);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await _sut.PerformRecurringPlanningAsync();

        // Assert
        _managerMock.Verify(manager => manager
            .ScheduleNewOrchestrationInstanceAsync(
                RecurringPlannerHandler.RecurringJobIdentity,
                uniqueName,
                _fixture.DkFirstOfDecember2024At1200.ToInstant()));
        _managerMock.Verify(manager => manager
            .ScheduleNewOrchestrationInstanceAsync(
                RecurringPlannerHandler.RecurringJobIdentity,
                uniqueName,
                _fixture.DkFirstOfDecember2024At1700.ToInstant()));
        _managerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenRecurringOrchestrationDescriptionPlannedFor12and17_WhenAllOrchestrationInstancesAreScheduled_ThenNoNewOrchestrationInstancesAreScheduled()
    {
        // Arrange
        var uniqueName = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var orchestrationDescription = CreateOrchestrationDescription(
            uniqueName,
            recurringCronExpression: "0 12,17 * * *");

        var scheduledToRun01 = CreateUserInitiatedOrchestrationInstance(
            orchestrationDescription,
            runAt: _fixture.DkFirstOfDecember2024At1200.ToInstant());

        var scheduledToRun02 = CreateUserInitiatedOrchestrationInstance(
            orchestrationDescription,
            runAt: _fixture.DkFirstOfDecember2024At1700.ToInstant());

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(orchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(scheduledToRun01);
            writeDbContext.OrchestrationInstances.Add(scheduledToRun02);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await _sut.PerformRecurringPlanningAsync();

        // Assert
        _managerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenRecurringOrchestrationDescriptionPlannedFor12and17_WhenOrchestrationInstanceAt12IsScheduled_ThenNewOrchestrationInstanceAt17IsScheduled()
    {
        // Arrange
        var uniqueName = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var orchestrationDescription = CreateOrchestrationDescription(
            uniqueName,
            recurringCronExpression: "0 12,17 * * *");

        var scheduledToRun01 = CreateUserInitiatedOrchestrationInstance(
            orchestrationDescription,
            runAt: _fixture.DkFirstOfDecember2024At1200.ToInstant());

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(orchestrationDescription);
            writeDbContext.OrchestrationInstances.Add(scheduledToRun01);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await _sut.PerformRecurringPlanningAsync();

        // Assert
        _managerMock.Verify(manager => manager
            .ScheduleNewOrchestrationInstanceAsync(
                RecurringPlannerHandler.RecurringJobIdentity,
                uniqueName,
                _fixture.DkFirstOfDecember2024At1700.ToInstant()));
        _managerMock.VerifyNoOtherCalls();
    }
}
