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

using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.DomainTestDataFactory;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Application.Orchestration;

public class IStartOrchestrationInstanceMessageCommandsTests :
    IClassFixture<ProcessManagerCoreFixture>,
    IAsyncLifetime
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

        _actorIdentity = EnergySupplier.ActorIdentity;

        _executorMock = new Mock<IOrchestrationInstanceExecutor>();
        _serviceProvider = ProcessManagerCoreServiceProviderFactory.BuildServiceProvider(
            _fixture.DatabaseManager.ConnectionString,
            services =>
            {
                services.AddScoped<IOrchestrationInstanceExecutor>(_ => _executorMock.Object);
            });

        _orchestrationRegister = _serviceProvider.GetRequiredService<IOrchestrationRegister>();
        _sut = _serviceProvider.GetRequiredService<IStartOrchestrationInstanceMessageCommands>();
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
    public async Task
        Given_MeteringPointId_When_StartNewOrchestrationInstanceAsync_Then_OrchestrationInstanceContainsMeteringPointId()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstanceId = await _sut.StartNewOrchestrationInstanceAsync(
            _actorIdentity,
            orchestrationDescription.UniqueName,
            new OrchestrationParameter("inputString"),
            [],
            IdempotencyKey.CreateNew(),
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
            new OrchestrationParameter("inputString"),
            [],
            IdempotencyKey.CreateNew(),
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

    [Fact]
    public async Task
        Given_NonDurableFunctionDescription_When_StartNewOrchestrationInstanceAsync_Then_ExecutorIsNotInvoked()
    {
        var orchestrationDescription = CreateOrchestrationDescription(isDurableFunction: false);
        await _orchestrationRegister.RegisterOrUpdateAsync(orchestrationDescription, "anyHostName");

        var orchestrationInstanceId = await _sut.StartNewOrchestrationInstanceAsync(
            _actorIdentity,
            orchestrationDescription.UniqueName,
            new OrchestrationParameter("inputString"),
            [],
            IdempotencyKey.CreateNew(),
            new ActorMessageId("actorMessageId"),
            new TransactionId("transactionId"),
            new MeteringPointId("meteringPointId"));

        orchestrationInstanceId.Value.Should().NotBeEmpty();
        _executorMock.Invocations.Should().BeEmpty();
        _executorMock.VerifyNoOtherCalls();
    }
}
