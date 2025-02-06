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
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Application.Orchestration;

public class IStartOrchestrationInstanceMessageCommandsTests : IClassFixture<ProcessManagerCoreFixture>, IAsyncLifetime
{
    private readonly ProcessManagerCoreFixture _fixture;

    private readonly ActorIdentity _actorIdentity;

    private readonly Mock<IOrchestrationInstanceExecutor> _executorMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly IOrchestrationRegister _orchestrationRegister;

    private readonly IStartOrchestrationInstanceMessageCommands _sut;

    public IStartOrchestrationInstanceMessageCommandsTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;

        _actorIdentity = new ActorIdentity(new ActorId(Guid.NewGuid()));

        _executorMock = new Mock<IOrchestrationInstanceExecutor>();

        var services = ConfigureServices(_fixture, _executorMock);
        _serviceProvider = services.BuildServiceProvider();

        _orchestrationRegister = _serviceProvider.GetRequiredService<IOrchestrationRegister>();
        _sut = _serviceProvider.GetRequiredService<IStartOrchestrationInstanceMessageCommands>();
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
    public async Task
        Given_MeteringPointId_When_StartNewOrchestrationInstanceAsync_Then_OrchestrationInstanceContainsMeteringPointId()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstanceId = await _sut.StartNewOrchestrationInstanceAsync(
            _actorIdentity,
            orchestrationDescription.UniqueName,
            new TestOrchestrationParameter("inputString"),
            [],
            new IdempotencyKey(Guid.NewGuid().ToString()),
            new ActorMessageId("actorMessageId"),
            new TransactionId("transactionId"),
            new MeteringPointId("meteringPointId"));

        orchestrationInstanceId.Value.Should().NotBeEmpty();
        _executorMock.Verify(
            x => x.StartNewOrchestrationInstanceAsync(
                It.Is<OrchestrationDescription>(od => od.UniqueName == orchestrationDescription.UniqueName),
                It.Is<OrchestrationInstance>(
                    oi => oi.ActorMessageId!.Value == "actorMessageId"
                          && oi.TransactionId!.Value == "transactionId"
                          && oi.MeteringPointId!.Value == "meteringPointId")),
            Times.Once);

        _executorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task
        Given_NoMeteringPointId_When_StartNewOrchestrationInstanceAsync_Then_OrchestrationInstanceContainsNoMeteringPointId()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstanceId = await _sut.StartNewOrchestrationInstanceAsync(
            _actorIdentity,
            orchestrationDescription.UniqueName,
            new TestOrchestrationParameter("inputString"),
            [],
            new IdempotencyKey(Guid.NewGuid().ToString()),
            new ActorMessageId("actorMessageId"),
            new TransactionId("transactionId"),
            null);

        orchestrationInstanceId.Value.Should().NotBeEmpty();
        _executorMock.Verify(
            x => x.StartNewOrchestrationInstanceAsync(
                It.Is<OrchestrationDescription>(od => od.UniqueName == orchestrationDescription.UniqueName),
                It.Is<OrchestrationInstance>(
                    oi => oi.ActorMessageId!.Value == "actorMessageId"
                          && oi.TransactionId!.Value == "transactionId"
                          && oi.MeteringPointId == null)),
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

        services.AddInMemoryConfiguration(
            new Dictionary<string, string?>
            {
                [$"{ProcessManagerOptions.SectionName}:{nameof(ProcessManagerOptions.SqlDatabaseConnectionString)}"]
                    = fixture.DatabaseManager.ConnectionString,
                [$"{nameof(ProcessManagerTaskHubOptions.ProcessManagerStorageConnectionString)}"]
                    = "Not used, but cannot be empty",
                [$"{nameof(ProcessManagerTaskHubOptions.ProcessManagerTaskHubName)}"]
                    = "Not used, but cannot be empty",
            });
        services.AddProcessManagerCore();

        // Additional registration to ensure we can keep the database consistent by adding orchestration descriptions
        services.AddTransient<IOrchestrationRegister, OrchestrationRegister>();

        return services;
    }

    private static OrchestrationDescription CreateOrchestrationDescription()
    {
        var orchestrationDescription = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(Guid.NewGuid().ToString(), 1),
            canBeScheduled: true,
            functionName: "TestOrchestrationFunction");

        orchestrationDescription.ParameterDefinition.SetFromType<TestOrchestrationParameter>();

        return orchestrationDescription;
    }

    public sealed record TestOrchestrationParameter(string InputString);
}
