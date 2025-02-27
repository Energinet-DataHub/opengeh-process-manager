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
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Application.Orchestration;

public class INotifyOrchestrationInstanceCommandsTests : IClassFixture<ProcessManagerCoreFixture>, IAsyncLifetime
{
    private readonly ProcessManagerCoreFixture _fixture;

    private readonly ActorIdentity _actorIdentity;

    private readonly Mock<IOrchestrationInstanceExecutor> _executorMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly IOrchestrationRegister _orchestrationRegister;
    private readonly IOrchestrationInstanceRepository _orchestrationInstanceRepository;

    private readonly INotifyOrchestrationInstanceCommands _sut;

    public INotifyOrchestrationInstanceCommandsTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;

        _actorIdentity = new ActorIdentity(new Actor(ActorNumber.Create("1234567890123"), ActorRole.EnergySupplier));

        _executorMock = new Mock<IOrchestrationInstanceExecutor>();

        var services = ConfigureServices(_fixture, _executorMock);
        _serviceProvider = services.BuildServiceProvider();

        _orchestrationRegister = _serviceProvider.GetRequiredService<IOrchestrationRegister>();
        _orchestrationInstanceRepository = _serviceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        _sut = _serviceProvider.GetRequiredService<INotifyOrchestrationInstanceCommands>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Disabling OrchestrationDescriptions so tests doesn't interfere with each other
        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        await dbContext.OrchestrationDescriptions.ForEachAsync(item => item.IsEnabled = false);
        await dbContext.SaveChangesAsync();

        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Given_UnknownOrchestrationInstance_When_NotifyOrchestrationInstanceAsync_Then_ThrowException()
    {
        var act = async () => await _sut.NotifyOrchestrationInstanceAsync(
            new OrchestrationInstanceId(Guid.NewGuid()),
            "anyEvent",
            new TestOrchestrationParameter("inputString"));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Orchestration instance (Id=*) to notify was not found.");

        _executorMock.Invocations.Should().BeEmpty();
        _executorMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_UnknownOrchestrationDescription_When_NotifyOrchestrationInstanceAsync_Then_ThrowException(
        bool isDurableFunction)
    {
        // Creating an instance from a description is the easiest way to make one.
        // We have, however, not persisted the description and as such it does not exist.
        var orchestrationDescription = CreateOrchestrationDescription(isDurableFunction);

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            _actorIdentity,
            orchestrationDescription,
            [],
            SystemClock.Instance);

        await _orchestrationInstanceRepository.AddAsync(orchestrationInstance);

        var act = async () => await _sut.NotifyOrchestrationInstanceAsync(
            orchestrationInstance.Id,
            "anyEvent",
            new TestOrchestrationParameter("inputString"));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Orchestration instance (Id=*) to notify was not found.");

        _executorMock.Invocations.Should().BeEmpty();
        _executorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_NonDurableFunctionInstance_When_NotifyOrchestrationInstanceAsync_Then_NothingHappens()
    {
        var orchestrationDescription = CreateOrchestrationDescription(false);
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            _actorIdentity,
            orchestrationDescription,
            [],
            SystemClock.Instance);

        await _orchestrationInstanceRepository.AddAsync(orchestrationInstance);
        await _orchestrationInstanceRepository.UnitOfWork.CommitAsync(CancellationToken.None);

        var act = async () => await _sut.NotifyOrchestrationInstanceAsync(
            orchestrationInstance.Id,
            "anyEvent",
            new TestOrchestrationParameter("inputString"));

        await act.Should().NotThrowAsync();

        _executorMock.Invocations.Should().BeEmpty();
        _executorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_DurableFunctionInstance_When_NotifyOrchestrationInstanceAsync_Then_ExecutorIsInvoked()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            _actorIdentity,
            orchestrationDescription,
            [],
            SystemClock.Instance);

        await _orchestrationInstanceRepository.AddAsync(orchestrationInstance);
        await _orchestrationInstanceRepository.UnitOfWork.CommitAsync(CancellationToken.None);

        var act = async () => await _sut.NotifyOrchestrationInstanceAsync(
            orchestrationInstance.Id,
            "anyEvent",
            new TestOrchestrationParameter("inputString"));

        await act.Should().NotThrowAsync();

        _executorMock.Verify(
            x => x.NotifyOrchestrationInstanceAsync(
                orchestrationInstance.Id,
                "anyEvent",
                It.Is<TestOrchestrationParameter>(p => p.InputString == "inputString")),
            Times.Once);

        _executorMock.VerifyNoOtherCalls();
    }

    private static ServiceCollection ConfigureServices(
        ProcessManagerCoreFixture fixture,
        IMock<IOrchestrationInstanceExecutor> executorMock)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddNodaTimeForApplication();

        // Services we want to mock MUST be registered before we call Process Manager DI extensions because we always use "TryAdd" within those
        services.AddScoped<IOrchestrationInstanceExecutor>(_ => executorMock.Object);

        // App settings
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
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
                })
            .Build();

        services.AddScoped<IConfiguration>(_ => configuration);
        services.AddProcessManagerCore(configuration);

        // Additional registration to ensure we can keep the database consistent by adding orchestration descriptions
        services.AddTransient<IOrchestrationRegister, OrchestrationRegister>();

        return services;
    }

    private static OrchestrationDescription CreateOrchestrationDescription(bool isDurableFunction = true)
    {
        var orchestrationDescription = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1),
            canBeScheduled: true,
            functionName: isDurableFunction ? "TestOrchestrationFunction" : string.Empty);

        orchestrationDescription.ParameterDefinition.SetFromType<TestOrchestrationParameter>();

        return orchestrationDescription;
    }

    public sealed record TestOrchestrationParameter(string InputString);
}
