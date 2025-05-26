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
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.CustomQueries.Examples.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to
/// perform a custom search for Calculations orchestration instances.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class SearchTrigger_ExampleById_V1Tests : IAsyncLifetime
{
    public SearchTrigger_ExampleById_V1Tests(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services
            .AddTokenCredentialProvider()
            .AddInMemoryConfiguration(new Dictionary<string, string?>
            {
                // Process Manager HTTP client
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                    = SubsystemAuthenticationOptionsForTests.ApplicationIdUri,
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                    = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
                [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                    = Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            });

        // Process Manager HTTP client
        services.AddProcessManagerHttpClients();

        ServiceProvider = services.BuildServiceProvider();

        ProcessManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    private IProcessManagerClient ProcessManagerClient { get; }

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
    /// This test proves that we can get type strong result objects return from the custom query.
    /// </summary>
    [Fact]
    public async Task Given_NoInputOrchestrationInstanceInDatabase_When_QueryById_Then_ReturnsExpectedResultType()
    {
        // Given
        // => Brs X01 Input example
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        var orchestrationInstanceId = await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_X01.NoInputExample.V1.Model.StartNoInputExampleCommandV1(
                    Fixture.DefaultUserIdentity),
                CancellationToken.None);

        // => Custom query
        var customQuery = new ExampleByIdQueryV1(
            Fixture.DefaultUserIdentity,
            orchestrationInstanceId);

        // When
        var actual = await ProcessManagerClient
            .SearchOrchestrationInstanceByCustomQueryAsync(
                customQuery,
                CancellationToken.None);

        // Then
        using var assertionScope = new AssertionScope();
        actual.Should()
            .BeOfType<NoInputExampleResultV1>()
            .Which.Id.Should().Be(orchestrationInstanceId);
    }

    /// <summary>
    /// This test proves that we can get "null" return from the custom query, if the type doesn't match supported Calculations.
    /// </summary>
    [Fact]
    public async Task Given_UnsupportedExampleOrchestrationInstanceInDatabase_When_QueryById_Then_ReturnsNull()
    {
        // Given
        // => Brs X03 Failing Orchestration Instance example
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        var orchestrationInstanceId = await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_X03.FailingOrchestrationInstanceExample.V1.Model.StartFailingOrchestrationInstanceExampleCommandV1(
                    Fixture.DefaultUserIdentity),
                CancellationToken.None);

        // => Custom query
        var customQuery = new ExampleByIdQueryV1(
            Fixture.DefaultUserIdentity,
            orchestrationInstanceId);

        // When
        var actual = await ProcessManagerClient
            .SearchOrchestrationInstanceByCustomQueryAsync(
                customQuery,
                CancellationToken.None);

        // Then
        actual.Should().BeNull();
    }
}
