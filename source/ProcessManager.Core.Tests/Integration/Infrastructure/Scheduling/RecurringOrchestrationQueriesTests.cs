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
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Scheduling;

[Collection(nameof(ProcessManagerCoreCollection))]
public class RecurringOrchestrationQueriesTests : IAsyncLifetime
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

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
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
        var actual = await _sut.GetAllRecurringAsync();

        // Assert
        actual.Should().ContainEquivalentOf(enabledOrchestrationDescriptionV2);
        actual.Should().NotContainEquivalentOf(enabledOrchestrationDescriptionV1);
        actual.Should().NotContainEquivalentOf(disabledOrchestrationDescriptionV1);
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
}
