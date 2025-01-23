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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.NoInputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Core.Infrastructure.Orchestration;

[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class DurableOrchestrationInstanceExecutorTests : IAsyncLifetime
{
    public DurableOrchestrationInstanceExecutorTests(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
        });
        services.AddProcessManagerHttpClients();
        ServiceProvider = services.BuildServiceProvider();
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.ExampleOrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
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
        await Fixture.DurableClient.StartNewAsync(brsX01NoInputDescription.FunctionName, orchestrationInstance.Id.Value.ToString());

        // Act
        var act = () => Fixture.DurableClient.StartNewAsync(brsX01NoInputDescription.FunctionName, orchestrationInstance.Id.Value.ToString());

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
        await Fixture.DurableClient.StartNewAsync(brsX01NoInputDescription.FunctionName, orchestrationInstance.Id.Value.ToString());
        var originalStatus = await Fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());

        // => Wait for completion
        await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationInstance.Id.Value.ToString(),
            TimeSpan.FromSeconds(20));

        // Act
        await Fixture.DurableClient.StartNewAsync(brsX01NoInputDescription.FunctionName, orchestrationInstance.Id.Value.ToString());

        // Assert
        var actualStatus = await Fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());
        actualStatus.CreatedTime.Should().NotBe(originalStatus.CreatedTime);
    }

    [Fact]
    public async Task Given_StartNewOrchestrationInstanceAsync_When_UsingNewId_Then_ReturnsTrueAndOrchestrationInstanceIsStarted()
    {
        // Arrange
        var executor = new DurableOrchestrationInstanceExecutor(
            new LoggerStub(),
            Fixture.DurableClient);

        var brsX01NoInputDescription = new OrchestrationDescriptionBuilder().Build();
        var orchestrationInstance = await SeedDatabaseAsync(brsX01NoInputDescription);

        // Act
        var actual = await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().BeTrue();

        var actualStatus = await Fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());
        actualStatus.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_StartNewOrchestrationInstanceAsync_When_UsingIdOfRunningOrchestrationInstance_Then_ReturnsFalseAndNoOrchestrationInstanceIsStarted()
    {
        // Arrange
        var executor = new DurableOrchestrationInstanceExecutor(
            new LoggerStub(),
            Fixture.DurableClient);

        var brsX01NoInputDescription = new OrchestrationDescriptionBuilder().Build();
        var orchestrationInstance = await SeedDatabaseAsync(brsX01NoInputDescription);

        // => Start
        await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);
        var originalStatus = await Fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());

        // Act
        var actual = await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().BeFalse();

        var actualStatus = await Fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());
        actualStatus.CreatedTime.Should().Be(originalStatus.CreatedTime);
    }

    [Fact]
    public async Task Given_StartNewOrchestrationInstanceAsync_When_UsingIdOfCompletedOrchestrationInstance_Then_ReturnsFalseAndNoOrchestrationInstanceIsStarted()
    {
        // Arrange
        var executor = new DurableOrchestrationInstanceExecutor(
            new LoggerStub(),
            Fixture.DurableClient);

        var brsX01NoInputDescription = new OrchestrationDescriptionBuilder().Build();
        var orchestrationInstance = await SeedDatabaseAsync(brsX01NoInputDescription);

        // => Start
        await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);
        var originalStatus = await Fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());

        // => Wait for completion
        await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationInstance.Id.Value.ToString(),
            TimeSpan.FromSeconds(20));

        // Act
        var actual = await executor.StartNewOrchestrationInstanceAsync(brsX01NoInputDescription, orchestrationInstance);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().BeFalse();

        var actualStatus = await Fixture.DurableClient.GetStatusAsync(orchestrationInstance.Id.Value.ToString());
        actualStatus.CreatedTime.Should().Be(originalStatus.CreatedTime);
    }

    private async Task<OrchestrationInstance> SeedDatabaseAsync(OrchestrationDescription brsX01NoInputDescription)
    {
        var operatingIdentity = new UserIdentity(
            new UserId(Guid.NewGuid()),
            new ActorId(Guid.NewGuid()));

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            operatingIdentity,
            brsX01NoInputDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);

        // Ensure orchestration instance can be set to Running later
        orchestrationInstance.Lifecycle.TransitionToQueued(SystemClock.Instance);

        await using (var writeDbContext = Fixture.ExampleOrchestrationsAppManager.DatabaseManager.CreateDbContext())
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
