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

using DurableTask.Core.Exceptions;
using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using NodaTime;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Orchestration;

[Collection(nameof(ProcessManagerCoreAppCollection))]
public class DurableOrchestrationInstanceExecutorTests : IAsyncLifetime
{
    private readonly ProcessManagerCoreAppFixture _fixture;

    public DurableOrchestrationInstanceExecutorTests(
        ProcessManagerCoreAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);
    }

    public Task InitializeAsync()
    {
        _fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        _fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.ExampleOrchestrationsAppManager.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    /// <summary>
    /// A test to prove that Durable Client throws an exception if we try to start
    /// an orchestration using an instance ID of an already running orchestration.
    ///
    /// We used this knowledge to design idempotency in the DurableOrchestrationInstanceExecutor.
    /// </summary>
    [Fact]
    public async Task Given_StartNewAsync_When_UsingIdOfRunningOrchestrationInstance_Then_ThrowsExpectedException()
    {
        // Arrange
        var brsX01NoInputDescription = new OrchestrationDescriptionBuilder().Build();
        var orchestrationInstance = await SeedDatabaseAsync(brsX01NoInputDescription);

        // => Start
        await _fixture.DurableClient.StartNewAsync(brsX01NoInputDescription.FunctionName, orchestrationInstance.Id.Value.ToString());

        // Act
        var act = () => _fixture.DurableClient.StartNewAsync(brsX01NoInputDescription.FunctionName, orchestrationInstance.Id.Value.ToString());

        // Assert
        await act.Should().ThrowAsync<OrchestrationAlreadyExistsException>();
    }

    /// <summary>
    /// A test to prove that Durable Client can start an orchestration if we start
    /// it using an instance ID of an completed orchestration.
    ///
    /// We used this knowledge to design idempotency in the DurableOrchestrationInstanceExecutor.
    /// </summary>
    [Fact]
    public async Task Given_StartNewAsync_When_UsingIdOfCompletedOrchestrationInstance_Then_OrchestrationIsStarted()
    {
        // Arrange
        var brsX01NoInputDescription = new OrchestrationDescriptionBuilder().Build();
        var orchestrationInstance = await SeedDatabaseAsync(brsX01NoInputDescription);

        // => Start
        await _fixture.DurableClient.StartNewAsync(brsX01NoInputDescription.FunctionName, orchestrationInstance.Id.Value.ToString());
        var originalStatus = await _fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());

        // => Wait for completion
        await _fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationInstance.Id.Value.ToString(),
            TimeSpan.FromSeconds(20));

        // Act
        await _fixture.DurableClient.StartNewAsync(brsX01NoInputDescription.FunctionName, orchestrationInstance.Id.Value.ToString());

        // Assert
        var actualStatus = await _fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());
        actualStatus.CreatedTime.Should().NotBe(originalStatus.CreatedTime);
    }

    [Fact]
    public async Task Given_StartNewOrchestrationInstanceAsync_When_UsingNewId_Then_ReturnsTrueAndOrchestrationInstanceIsStarted()
    {
        // Arrange
        var executor = new DurableOrchestrationInstanceExecutor(
            new LoggerStub(),
            _fixture.DurableClient);

        var brsX01NoInputDescription = new OrchestrationDescriptionBuilder().Build();
        var orchestrationInstance = await SeedDatabaseAsync(brsX01NoInputDescription);

        // Act
        var actual = await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().BeTrue();

        var actualStatus = await _fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());
        actualStatus.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_StartNewOrchestrationInstanceAsync_When_UsingIdOfRunningOrchestrationInstance_Then_ReturnsFalseAndNoOrchestrationInstanceIsStarted()
    {
        // Arrange
        var executor = new DurableOrchestrationInstanceExecutor(
            new LoggerStub(),
            _fixture.DurableClient);

        var brsX01NoInputDescription = new OrchestrationDescriptionBuilder().Build();
        var orchestrationInstance = await SeedDatabaseAsync(brsX01NoInputDescription);

        // => Start
        await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);
        var originalStatus = await _fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());

        // Act
        var actual = await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().BeFalse();

        var actualStatus = await _fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());
        actualStatus.CreatedTime.Should().Be(originalStatus.CreatedTime);
    }

    [Fact]
    public async Task Given_StartNewOrchestrationInstanceAsync_When_UsingIdOfCompletedOrchestrationInstance_Then_ReturnsFalseAndNoOrchestrationInstanceIsStarted()
    {
        // Arrange
        var executor = new DurableOrchestrationInstanceExecutor(
            new LoggerStub(),
            _fixture.DurableClient);

        var brsX01NoInputDescription = new OrchestrationDescriptionBuilder().Build();
        var orchestrationInstance = await SeedDatabaseAsync(brsX01NoInputDescription);

        // => Start
        await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);
        var originalStatus = await _fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());

        // => Wait for completion
        await _fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationInstance.Id.Value.ToString(),
            TimeSpan.FromSeconds(20));

        // Act
        var actual = await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().BeFalse();

        var actualStatus = await _fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());
        actualStatus.CreatedTime.Should().Be(originalStatus.CreatedTime);
    }

    private async Task<OrchestrationInstance> SeedDatabaseAsync(OrchestrationDescription brsX01NoInputDescription)
    {
        var operatingIdentity = ProcessManagerDomainTestDataFactory.EnergySupplier.UserIdentity;

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            operatingIdentity,
            brsX01NoInputDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            actorMessageId: new ActorMessageId(Guid.NewGuid().ToString()),
            transactionId: new TransactionId(Guid.NewGuid().ToString()),
            meteringPointId: new MeteringPointId(Guid.NewGuid().ToString()));

        // Ensure orchestration instance can be set to Running later
        orchestrationInstance.Lifecycle.TransitionToQueued(SystemClock.Instance);

        await using (var writeDbContext = _fixture.ExampleOrchestrationsAppManager.DatabaseManager.CreateDbContext())
        {
            writeDbContext.OrchestrationDescriptions.Add(brsX01NoInputDescription);
            writeDbContext.OrchestrationInstances.Add(orchestrationInstance);
            await writeDbContext.SaveChangesAsync();
        }

        return orchestrationInstance;
    }

    private class LoggerStub : ILogger<DurableOrchestrationInstanceExecutor>
    {
        /// <summary>
        /// Number of times Log method is called.
        /// </summary>
        public int LogCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LogCount++;
        }
    }
}
