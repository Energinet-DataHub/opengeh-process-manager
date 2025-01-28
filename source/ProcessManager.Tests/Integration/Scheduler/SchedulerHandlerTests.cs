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

using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;
using Energinet.DataHub.ProcessManager.Scheduler;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Writers;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Tests.Integration.Scheduler;

public class SchedulerHandlerTests : IClassFixture<SchedulerHandlerFixture>, IAsyncLifetime
{
    private readonly SchedulerHandlerFixture _fixture;
    private readonly Mock<IOrchestrationInstanceExecutor> _executorMock;
    private readonly ServiceProvider _serviceProvider;

    public SchedulerHandlerTests(SchedulerHandlerFixture fixture)
    {
        _fixture = fixture;

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddScoped<IClock>(_ => _fixture.ClockMock.Object);
        services.AddNodaTimeForApplication();

        // Service we want to mock MUST be registered before we call Process Manager DI extensions because we always use "TryAdd" within those
        _executorMock = new Mock<IOrchestrationInstanceExecutor>();
        services.AddScoped<IOrchestrationInstanceExecutor>(_ => _executorMock.Object);

        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerOptions.SectionName}:{nameof(ProcessManagerOptions.SqlDatabaseConnectionString)}"]
                = _fixture.DatabaseManager.ConnectionString,
            [$"{nameof(ProcessManagerTaskHubOptions.ProcessManagerStorageConnectionString)}"]
                = "Not used, but cannot be empty",
            [$"{nameof(ProcessManagerTaskHubOptions.ProcessManagerTaskHubName)}"]
                = "Not used, but cannot be empty",
        });
        services.AddProcessManagerCore();

        // Additional registration to ensure we can keep the database consistent by adding orchestration descriptions
        services.AddTransient<IOrchestrationRegister, OrchestrationRegister>();

        // Register SUT
        services.AddScoped<SchedulerHandler>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Disabling OrchestrationDescriptions so tests doesn't interfere with each other
        using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        await dbContext.OrchestrationDescriptions.ForEachAsync(item => item.IsEnabled = false);
        await dbContext.SaveChangesAsync();

        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Given_OrchestrationInstancesScheduledToRun_When_SchedulerHandlerIsExecuted_Then_ExpectedOrchestrationInstancesAreQueued()
    {
        // Arrange
        var now = _fixture.ClockMock.Object.GetCurrentInstant();
        var userIdentity = new UserIdentity(
            new UserId(Guid.NewGuid()),
            new ActorId(Guid.NewGuid()));

        OrchestrationInstanceId scheduledInstanceId01;
        OrchestrationInstanceId scheduledInstanceId02;

        using (var arrangeScope = _serviceProvider.CreateScope())
        {
            var register = arrangeScope.ServiceProvider.GetRequiredService<IOrchestrationRegister>();
            var commands = arrangeScope.ServiceProvider.GetRequiredService<IStartOrchestrationInstanceCommands>();

            var orchestrationDescription = CreateOrchestrationDescription();
            await register.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

            scheduledInstanceId01 = await commands.ScheduleNewOrchestrationInstanceAsync(
                userIdentity,
                orchestrationDescription.UniqueName,
                runAt: now.PlusMinutes(-10));
            scheduledInstanceId02 = await commands.ScheduleNewOrchestrationInstanceAsync(
                userIdentity,
                orchestrationDescription.UniqueName,
                runAt: now.PlusMinutes(-5));
        }

        // Act
        var sut = _serviceProvider.GetRequiredService<SchedulerHandler>();
        await sut.StartScheduledOrchestrationInstancesAsync();

        // Assert
        using (var assertScope = _serviceProvider.CreateScope())
        {
            var queries = assertScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceQueries>();

            var scheduledInstance01 = await queries.GetAsync(scheduledInstanceId01);
            scheduledInstance01.Lifecycle.State.Should().Be(OrchestrationInstanceLifecycleState.Queued);

            var scheduledInstance02 = await queries.GetAsync(scheduledInstanceId02);
            scheduledInstance02.Lifecycle.State.Should().Be(OrchestrationInstanceLifecycleState.Queued);
        }
    }

    [Fact]
    public void Given_OrchestrationInstancesScheduledToRun_When_SchedulerHandlerIsExecuted_Then_ValidOrchestrationInstancesAreStartedEvenIfOthersAreFailing()
    {
        // 2 skedulered, 1 fejl behæftet => måske fejl i durable client
        // kør skeduler!
        // => bevis 1 af dem bliver skedulered
        // tilføj 1 skeduleret til databasen
        // kør skeduler igen!, den gamle fejler stadig, den nye bliver skeduleret
    }

    private static OrchestrationDescription CreateOrchestrationDescription()
    {
        var orchestrationDescription = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1),
            canBeScheduled: true,
            functionName: "TestOrchestrationFunction");

        return orchestrationDescription;
    }
}
