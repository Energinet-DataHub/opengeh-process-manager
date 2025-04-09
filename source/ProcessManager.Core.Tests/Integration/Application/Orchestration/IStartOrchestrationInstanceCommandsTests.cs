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
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Application.Orchestration;

public class IStartOrchestrationInstanceCommandsTests : IClassFixture<ProcessManagerDatabaseFixture>, IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;

    private readonly ActorIdentity _actorIdentity;

    private readonly Mock<IOrchestrationInstanceExecutor> _executorMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly IOrchestrationRegister _orchestrationRegister;

    private readonly IStartOrchestrationInstanceCommands _sut;

    public IStartOrchestrationInstanceCommandsTests(ProcessManagerDatabaseFixture fixture)
    {
        _fixture = fixture;

        _actorIdentity = new ActorIdentity(new Actor(ActorNumber.Create("1234567890123"), ActorRole.EnergySupplier));

        _executorMock = new Mock<IOrchestrationInstanceExecutor>();

        var services = ConfigureServices(_fixture, _executorMock);
        _serviceProvider = services.BuildServiceProvider();

        _orchestrationRegister = _serviceProvider.GetRequiredService<IOrchestrationRegister>();
        _sut = _serviceProvider.GetRequiredService<IStartOrchestrationInstanceCommands>();
    }

    public async Task InitializeAsync()
    {
        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        await dbContext.OrchestrationInstances.ExecuteDeleteAsync();
        await dbContext.OrchestrationDescriptions.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task
        Given_DurableFunctionWithoutInput_When_StartNewOrchestrationInstanceAsync_Then_ExecutorInvoked()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstanceId = await _sut.StartNewOrchestrationInstanceAsync(
            _actorIdentity,
            orchestrationDescription.UniqueName);

        orchestrationInstanceId.Value.Should().NotBeEmpty();
        _executorMock.Verify(
            x => x.StartNewOrchestrationInstanceAsync(
                It.Is<OrchestrationDescription>(od => od.UniqueName == orchestrationDescription.UniqueName),
                It.Is<OrchestrationInstance>(oi => oi.OrchestrationDescriptionId == orchestrationDescription.Id)),
            Times.Once);

        _executorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task
        Given_NonDurableFunctionWithoutInput_When_StartNewOrchestrationInstanceAsync_Then_ExecutorIsNotInvoked()
    {
        var orchestrationDescription = CreateOrchestrationDescription(false);
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstanceId = await _sut.StartNewOrchestrationInstanceAsync(
            _actorIdentity,
            orchestrationDescription.UniqueName);

        orchestrationInstanceId.Value.Should().NotBeEmpty();
        _executorMock.Invocations.Should().BeEmpty();
        _executorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task
        Given_DurableFunctionWithInput_When_StartNewOrchestrationInstanceAsync_Then_ExecutorInvoked()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstanceId = await _sut.StartNewOrchestrationInstanceAsync(
            _actorIdentity,
            orchestrationDescription.UniqueName,
            new TestOrchestrationParameter("inputString"),
            []);

        orchestrationInstanceId.Value.Should().NotBeEmpty();
        _executorMock.Verify(
            x => x.StartNewOrchestrationInstanceAsync(
                It.Is<OrchestrationDescription>(od => od.UniqueName == orchestrationDescription.UniqueName),
                It.Is<OrchestrationInstance>(
                    oi => oi.OrchestrationDescriptionId == orchestrationDescription.Id
                          && oi.ParameterValue.SerializedValue == "{\"InputString\":\"inputString\"}")),
            Times.Once);

        _executorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task
        Given_NonDurableFunctionWithInput_When_StartNewOrchestrationInstanceAsync_Then_ExecutorIsNotInvoked()
    {
        var orchestrationDescription = CreateOrchestrationDescription(false);
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstanceId = await _sut.StartNewOrchestrationInstanceAsync(
            _actorIdentity,
            orchestrationDescription.UniqueName,
            new TestOrchestrationParameter("inputString"),
            []);

        orchestrationInstanceId.Value.Should().NotBeEmpty();
        _executorMock.Invocations.Should().BeEmpty();
        _executorMock.VerifyNoOtherCalls();
    }

    private static ServiceCollection ConfigureServices(
        ProcessManagerDatabaseFixture fixture,
        IMock<IOrchestrationInstanceExecutor> executorMock)
    {
        var services = new ServiceCollection();

        services.AddLogging();

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
