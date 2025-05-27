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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.CustomQueries.Examples.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to
/// perform a custom search for Examples orchestration instances.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class SearchTrigger_Examples_V1Tests : IAsyncLifetime
{
    public SearchTrigger_Examples_V1Tests(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.ExampleOrchestrationsAppManager.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    /// <summary>
    /// This test proves that we can get type strong result objects return from the custom query.
    /// </summary>
    [Fact]
    public async Task ExamplesOrchestrationInstancesInDatabase_WhenQueryExamples_ExpectedResultTypesAreRetrieved()
    {
        // Arrange

        // => Brs X01 Input example
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        await Fixture.ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_X01.InputExample.V1.Model.StartInputExampleCommandV1(
                    Fixture.DefaultUserIdentity,
                    new Abstractions.Processes.BRS_X01.InputExample.V1.Model.InputV1(
                        ShouldSkipSkippableStep: false)),
                CancellationToken.None);

        // => Brs X01 No Input example
        // Start new orchestration instance (we don't have to wait for it, we just need data in the database)
        await Fixture.ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new Abstractions.Processes.BRS_X01.NoInputExample.V1.Model.StartNoInputExampleCommandV1(
                    Fixture.DefaultUserIdentity),
                CancellationToken.None);

        // => Custom query
        var customQuery = new ExamplesQueryV1(Fixture.DefaultUserIdentity)
        {
            LifecycleStates = [
                OrchestrationInstanceLifecycleState.Queued,
                OrchestrationInstanceLifecycleState.Running,
                OrchestrationInstanceLifecycleState.Terminated],
        };

        // Act
        var actual = await Fixture.ProcessManagerClient
            .SearchOrchestrationInstancesByCustomQueryAsync(
                customQuery,
                CancellationToken.None);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should()
            .Contain(x =>
                x.GetType() == typeof(InputExampleResultV1))
            .And.Contain(x =>
                x.GetType() == typeof(NoInputExampleResultV1));
    }
}
