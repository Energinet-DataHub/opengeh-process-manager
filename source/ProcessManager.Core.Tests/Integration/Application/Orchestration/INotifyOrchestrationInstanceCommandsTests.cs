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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;
using static Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.DomainTestDataFactory;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Application.Orchestration;

public class INotifyOrchestrationInstanceCommandsTests :
    IClassFixture<ProcessManagerCoreFixture>,
    IAsyncLifetime
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

        _actorIdentity = EnergySupplier.ActorIdentity;

        _executorMock = new Mock<IOrchestrationInstanceExecutor>();
        _serviceProvider = ProcessManagerCoreServiceProviderFactory.BuildServiceProvider(
            _fixture.DatabaseManager.ConnectionString,
            services =>
            {
                services.AddScoped<IOrchestrationInstanceExecutor>(_ => _executorMock.Object);
            });

        _orchestrationRegister = _serviceProvider.GetRequiredService<IOrchestrationRegister>();
        _orchestrationInstanceRepository = _serviceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        _sut = _serviceProvider.GetRequiredService<INotifyOrchestrationInstanceCommands>();
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
    public async Task Given_UnknownOrchestrationInstance_When_NotifyOrchestrationInstanceAsync_Then_ThrowException()
    {
        var act = async () => await _sut.NotifyOrchestrationInstanceAsync(
            new OrchestrationInstanceId(Guid.NewGuid()),
            "anyEvent",
            new OrchestrationParameter("inputString"));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Orchestration instance (Id=*) to notify was not found.");

        _executorMock.Invocations.Should().BeEmpty();
        _executorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Given_NonDurableFunctionInstance_When_NotifyOrchestrationInstanceAsync_Then_NothingHappens()
    {
        var orchestrationDescription = CreateOrchestrationDescription(isDurableFunction: false);
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
            new OrchestrationParameter("inputString"));

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
            new OrchestrationParameter("inputString"));

        await act.Should().NotThrowAsync();

        _executorMock.Verify(
            x => x.NotifyOrchestrationInstanceAsync(
                orchestrationInstance.Id,
                "anyEvent",
                It.Is<OrchestrationParameter>(p => p.TestString == "inputString")),
            Times.Once);

        _executorMock.VerifyNoOtherCalls();
    }
}
