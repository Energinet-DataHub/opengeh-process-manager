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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;
using Energinet.DataHub.ProcessManager.Scheduler;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Tests.Integration.Scheduler;

public class SchedulerHandlerTests : IClassFixture<SchedulerHandlerFixture>, IAsyncLifetime
{
    private readonly SchedulerHandlerFixture _fixture;

    private readonly Instant _now;
    private readonly UserIdentity _userIdentity;

    private readonly Mock<IOrchestrationInstanceExecutor> _executorMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly IOrchestrationRegister _orchestrationRegister;
    private readonly IStartOrchestrationInstanceCommands _startCommands;
    private readonly SchedulerHandler _sut;

    public SchedulerHandlerTests(SchedulerHandlerFixture fixture)
    {
        _fixture = fixture;

        _now = _fixture.ClockMock.Object.GetCurrentInstant();
        _userIdentity = new UserIdentity(
            new UserId(Guid.NewGuid()),
            new Actor(ActorNumber.Create("1234567890123"), ActorRole.EnergySupplier));

        _executorMock = new Mock<IOrchestrationInstanceExecutor>();

        var services = ConfigureServices(_fixture, _executorMock);
        _serviceProvider = services.BuildServiceProvider();

        _orchestrationRegister = _serviceProvider.GetRequiredService<IOrchestrationRegister>();
        _startCommands = _serviceProvider.GetRequiredService<IStartOrchestrationInstanceCommands>();

        _sut = _serviceProvider.GetRequiredService<SchedulerHandler>();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _fixture.DatabaseManager.ExecuteDeleteOnEntitiesAsync();

        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Given_OrchestrationInstancesScheduledToRun_When_SchedulerHandlerIsExecuted_Then_BothAreQueued()
    {
        // Arrange
        var orchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var scheduledInstanceId01 = await _startCommands.ScheduleNewOrchestrationInstanceAsync(
            _userIdentity,
            orchestrationDescription.UniqueName,
            runAt: _now.PlusMinutes(-10));
        var scheduledInstanceId02 = await _startCommands.ScheduleNewOrchestrationInstanceAsync(
            _userIdentity,
            orchestrationDescription.UniqueName,
            runAt: _now.PlusMinutes(-5));

        // Act
        await _sut.StartScheduledOrchestrationInstancesAsync();

        // Assert
        // => Must use a new scope, otherwise we won't see database changes
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
    public async Task Given_OrchestrationInstancesScheduledToRunButExecutorFailsOnOne_When_SchedulerHandlerIsExecuted_Then_OneIsPendingAndOneIsQueued()
    {
        // Arrange
        var orchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var scheduledInstanceId01 = await _startCommands.ScheduleNewOrchestrationInstanceAsync(
            _userIdentity,
            orchestrationDescription.UniqueName,
            runAt: _now.PlusMinutes(-10));
        var scheduledInstanceId02 = await _startCommands.ScheduleNewOrchestrationInstanceAsync(
            _userIdentity,
            orchestrationDescription.UniqueName,
            runAt: _now.PlusMinutes(-5));

        // => Fail execution of "01"
        _executorMock
            .Setup(mock => mock
                .StartNewOrchestrationInstanceAsync(
                    It.IsAny<OrchestrationDescription>(),
                    It.Is<OrchestrationInstance>(oi => oi.Id == scheduledInstanceId01)))
            .ThrowsAsync(new Exception());

        // Act
        await _sut.StartScheduledOrchestrationInstancesAsync();

        // Assert
        // => Must use a new scope, otherwise we won't see database changes
        using (var assertScope = _serviceProvider.CreateScope())
        {
            var queries = assertScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceQueries>();

            var scheduledInstance01 = await queries.GetAsync(scheduledInstanceId01);
            scheduledInstance01.Lifecycle.State.Should().Be(OrchestrationInstanceLifecycleState.Pending);

            var scheduledInstance02 = await queries.GetAsync(scheduledInstanceId02);
            scheduledInstance02.Lifecycle.State.Should().Be(OrchestrationInstanceLifecycleState.Queued);
        }
    }

    /// <summary>
    /// The intention of this test is to prove that one failing orchestration instance won't keep the scheduler from
    /// beeing able to keep scheduling others non-failing orchestration instances.
    /// </summary>
    [Fact]
    public async Task Given_OrchestrationInstancesScheduledToRunButExecutorKeepsFailingOnOne_When_SchedulerHandlerIsExecutedRecurringly_Then_OnlyTheFailingOneIsPendingOthersCanBeQueued()
    {
        // Arrange
        var orchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var scheduledInstanceId01 = await _startCommands.ScheduleNewOrchestrationInstanceAsync(
            _userIdentity,
            orchestrationDescription.UniqueName,
            runAt: _now.PlusMinutes(-10));
        var scheduledInstanceId02 = await _startCommands.ScheduleNewOrchestrationInstanceAsync(
            _userIdentity,
            orchestrationDescription.UniqueName,
            runAt: _now.PlusMinutes(-5));

        // => Fail execution of "01"
        _executorMock
            .Setup(mock => mock
                .StartNewOrchestrationInstanceAsync(
                    It.IsAny<OrchestrationDescription>(),
                    It.Is<OrchestrationInstance>(oi => oi.Id == scheduledInstanceId01)))
            .ThrowsAsync(new Exception());

        // => First execution of scheduler
        await _sut.StartScheduledOrchestrationInstancesAsync();

        // => Schedule an additional orchestration instance
        var scheduledInstanceId03 = await _startCommands.ScheduleNewOrchestrationInstanceAsync(
            _userIdentity,
            orchestrationDescription.UniqueName,
            runAt: _now.PlusMinutes(-1));

        // Act
        await _sut.StartScheduledOrchestrationInstancesAsync();

        // Assert
        // => Must use a new scope, otherwise we won't see database changes
        using (var assertScope = _serviceProvider.CreateScope())
        {
            var queries = assertScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceQueries>();

            var scheduledInstance01 = await queries.GetAsync(scheduledInstanceId01);
            scheduledInstance01.Lifecycle.State.Should().Be(OrchestrationInstanceLifecycleState.Pending);

            var scheduledInstance02 = await queries.GetAsync(scheduledInstanceId02);
            scheduledInstance02.Lifecycle.State.Should().Be(OrchestrationInstanceLifecycleState.Queued);

            var scheduledInstance03 = await queries.GetAsync(scheduledInstanceId03);
            scheduledInstance03.Lifecycle.State.Should().Be(OrchestrationInstanceLifecycleState.Queued);
        }
    }

    private static ServiceCollection ConfigureServices(SchedulerHandlerFixture fixture, Mock<IOrchestrationInstanceExecutor> executorMock)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddScoped<IClock>(_ => fixture.ClockMock.Object);
        services.AddNodaTimeForApplication();

        // Services we want to mock MUST be registered before we call Process Manager DI extensions because we always use "TryAdd" within those
        services.AddScoped<IOrchestrationInstanceExecutor>(_ => executorMock.Object);

        // App settings
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProcessManagerOptions.SectionName}:{nameof(ProcessManagerOptions.SqlDatabaseConnectionString)}"]
                        = fixture.DatabaseManager.ConnectionString,
                [$"{nameof(ProcessManagerTaskHubOptions.ProcessManagerStorageConnectionString)}"]
                        = "Not used, but cannot be empty",
                [$"{nameof(ProcessManagerTaskHubOptions.ProcessManagerTaskHubName)}"]
                        = "Not used, but cannot be empty",
                [$"{AuthenticationOptions.SectionName}:{nameof(AuthenticationOptions.ApplicationIdUri)}"]
                        = "Not used, but cannot be empty",
                [$"{AuthenticationOptions.SectionName}:{nameof(AuthenticationOptions.Issuer)}"]
                        = "Not used, but cannot be empty",
            }).Build();
        services.AddScoped<IConfiguration>(_ => configuration);

        services.AddProcessManagerCore(configuration);

        // Additional registration to ensure we can keep the database consistent by adding orchestration descriptions
        services.AddTransient<IOrchestrationRegister, OrchestrationRegister>();

        // Register SUT
        services.AddScoped<SchedulerHandler>();

        return services;
    }
}
