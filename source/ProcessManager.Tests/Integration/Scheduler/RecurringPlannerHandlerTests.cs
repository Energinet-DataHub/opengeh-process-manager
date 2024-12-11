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

using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Scheduling;
using Energinet.DataHub.ProcessManager.Scheduler;
using Energinet.DataHub.ProcessManager.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Tests.Integration.Scheduler;

public class RecurringPlannerHandlerTests : IClassFixture<RecurringPlannerHandlerFixture>
{
    private readonly RecurringPlannerHandlerFixture _fixture;
    private readonly Mock<IClock> _clockMock;
    private readonly Mock<IStartOrchestrationInstanceCommands> _managerMock;
    private readonly ProcessManagerContext _dbContext;
    private readonly RecurringPlannerHandler _sut;

    public RecurringPlannerHandlerTests(RecurringPlannerHandlerFixture fixture)
    {
        _fixture = fixture;

        _clockMock = new Mock<IClock>();
        _managerMock = new Mock<IStartOrchestrationInstanceCommands>();

        _dbContext = _fixture.DatabaseManager.CreateDbContext();
        var queries = new RecurringOrchestrationQueries(_dbContext);

        _sut = new RecurringPlannerHandler(
            Mock.Of<ILogger<RecurringPlannerHandler>>(),
            _clockMock.Object,
            queries,
            _managerMock.Object);
    }

    [Fact]
    public async Task GivenRecurringOrchestrationDescriptionPlannedFor12and17_WhenNoExistingOrchestrationInstances_ThenExpectedOrchestrationInstancesAreScheduled()
    {
        // Arrange
        var timeIs1100 = Instant.FromUtc(2024, 12, 1, 11, 0);
        _clockMock.Setup(m => m.GetCurrentInstant())
            .Returns(timeIs1100);

        var uniqueName = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var cronExpressionPlanFor1200And1700 = "0 12,17 * * *";
        var enabledOrchestrationDescription = CreateOrchestrationDescription(uniqueName, cronExpressionPlanFor1200And1700);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(enabledOrchestrationDescription);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        await _sut.PerformRecurringPlanningAsync();

        // Assert
        _managerMock.Verify(manager => manager
            .ScheduleNewOrchestrationInstanceAsync(
                RecurringPlannerHandler.DatahubAdministratorActorId,
                uniqueName,
                Instant.FromUtc(2024, 12, 1, 12, 0)));
        _managerMock.Verify(manager => manager
            .ScheduleNewOrchestrationInstanceAsync(
                RecurringPlannerHandler.DatahubAdministratorActorId,
                uniqueName,
                Instant.FromUtc(2024, 12, 1, 17, 0)));
    }

    private static OrchestrationDescription CreateOrchestrationDescription(
        OrchestrationDescriptionUniqueName uniqueName,
        string? recurringCronExpression = default,
        bool isEnabled = true)
    {
        var orchestrationDescription = new OrchestrationDescription(
            uniqueName,
            canBeScheduled: true,
            functionName: "TestOrchestrationFunction");

        if (recurringCronExpression != null)
            orchestrationDescription.RecurringCronExpression = recurringCronExpression;

        orchestrationDescription.IsEnabled = isEnabled;

        return orchestrationDescription;
    }

    private static OrchestrationInstance CreateOrchestrationInstance(
        OrchestrationDescription orchestrationDescription,
        Instant runAt = default)
    {
        var userIdentity = new UserIdentity(
            new UserId(Guid.NewGuid()),
            new ActorId(Guid.NewGuid()));

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            userIdentity,
            orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            runAt: runAt);

        return orchestrationInstance;
    }
}
