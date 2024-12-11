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

using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Scheduling;

public class RecurringOrchestrationQueriesTests : IClassFixture<ProcessManagerCoreFixture>, IAsyncDisposable
{
    private readonly ProcessManagerCoreFixture _fixture;
    private readonly ProcessManagerContext _dbContext;
    private readonly RecurringOrchestrationQueries _sut;

    public RecurringOrchestrationQueriesTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;
        _dbContext = _fixture.DatabaseManager.CreateDbContext();
        _sut = new RecurringOrchestrationQueries(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GivenOrchestrationDescriptionsInDatabase_WhenGetAllRecurring_ThenExpectedOrchestrationDescriptionIsRetrieved()
    {
        // Arrange
        var enabledName = Guid.NewGuid().ToString();
        var enabledOrchestrationDescriptionV1 = CreateOrchestrationDescription(new OrchestrationDescriptionUniqueName(enabledName, 1));
        var enabledOrchestrationDescriptionV2 = CreateOrchestrationDescription(
            new OrchestrationDescriptionUniqueName(enabledName, 2),
            recurringCronExpression: "0 0 * * *");

        var disabledName = Guid.NewGuid().ToString();
        var disabledOrchestrationDescriptionV1 = CreateOrchestrationDescription(
            new OrchestrationDescriptionUniqueName(disabledName, 1),
            recurringCronExpression: "0 0 * * *",
            isEnabled: false);

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(enabledOrchestrationDescriptionV1);
            writeDbContext.OrchestrationDescriptions.Add(enabledOrchestrationDescriptionV2);
            writeDbContext.OrchestrationDescriptions.Add(disabledOrchestrationDescriptionV1);
            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchRecurringOrchestrationDescriptionsAsync();

        // Assert
        actual.Should().ContainEquivalentOf(enabledOrchestrationDescriptionV2);
        actual.Should().NotContainEquivalentOf(enabledOrchestrationDescriptionV1);
        actual.Should().NotContainEquivalentOf(disabledOrchestrationDescriptionV1);
    }

    [Fact]
    public async Task GivenOrchestrationInstancesInDatabase_WhenSearchScheduled_ThenExpectedOrchestrationInstancesAreRetrieved()
    {
        // Arrange
        var currentInstant = SystemClock.Instance.GetCurrentInstant();

        var uniqueName1 = new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1);
        var existingOrchestrationDescription01 = CreateOrchestrationDescription(uniqueName1);

        var scheduledToRunIn09 = CreateOrchestrationInstance(
            existingOrchestrationDescription01,
            runAt: currentInstant.PlusMinutes(09));
        var scheduledToRunIn10 = CreateOrchestrationInstance(
            existingOrchestrationDescription01,
            runAt: currentInstant.PlusMinutes(10));
        var scheduledToRunIn20_01 = CreateOrchestrationInstance(
            existingOrchestrationDescription01,
            runAt: currentInstant.PlusMinutes(20));
        var scheduledToRunIn30 = CreateOrchestrationInstance(
            existingOrchestrationDescription01,
            runAt: currentInstant.PlusMinutes(30));
        var scheduledToRunIn31 = CreateOrchestrationInstance(
            existingOrchestrationDescription01,
            runAt: currentInstant.PlusMinutes(31));
        var scheduledIntoTheFarFuture = CreateOrchestrationInstance(
            existingOrchestrationDescription01,
            runAt: currentInstant.PlusDays(5));

        var scheduledToRunIn10ButUserCanceled = CreateOrchestrationInstance(
            existingOrchestrationDescription01,
            runAt: currentInstant.PlusMinutes(10));
        scheduledToRunIn10ButUserCanceled.Lifecycle.TransitionToUserCanceled(
            SystemClock.Instance,
            new UserIdentity(
                new UserId(Guid.NewGuid()),
                new ActorId(Guid.NewGuid())));

        var existingOrchestrationDescription02 = CreateOrchestrationDescription(
            new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1));
        var scheduledToRunIn20_02 = CreateOrchestrationInstance(
            existingOrchestrationDescription02,
            runAt: currentInstant.PlusMinutes(20));

        await using (var writeDbContext = _fixture.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription01);
            writeDbContext.OrchestrationInstances.Add(scheduledToRunIn09);
            writeDbContext.OrchestrationInstances.Add(scheduledToRunIn10);
            writeDbContext.OrchestrationInstances.Add(scheduledToRunIn20_01);
            writeDbContext.OrchestrationInstances.Add(scheduledToRunIn30);
            writeDbContext.OrchestrationInstances.Add(scheduledToRunIn31);
            writeDbContext.OrchestrationInstances.Add(scheduledIntoTheFarFuture);
            writeDbContext.OrchestrationInstances.Add(scheduledToRunIn10ButUserCanceled);

            writeDbContext.OrchestrationDescriptions.Add(existingOrchestrationDescription02);
            writeDbContext.OrchestrationInstances.Add(scheduledToRunIn20_02);

            await writeDbContext.SaveChangesAsync();
        }

        // Act
        var actual = await _sut.SearchScheduledOrchestrationInstancesAsync(
            existingOrchestrationDescription01.UniqueName,
            runAtOrLater: currentInstant.PlusMinutes(10),
            runAtOrEarlier: currentInstant.PlusMinutes(30));

        // Assert
        actual.Should()
            .BeEquivalentTo(new[] { scheduledToRunIn10, scheduledToRunIn20_01, scheduledToRunIn30 });
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
        Instant? runAt = default)
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
