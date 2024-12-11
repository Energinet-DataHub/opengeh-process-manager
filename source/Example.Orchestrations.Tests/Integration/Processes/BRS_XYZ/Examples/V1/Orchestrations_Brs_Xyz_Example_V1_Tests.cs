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

using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_XYZ.Example.V1.Model;
using Energinet.DataHub.Example.Orchestrations.Processes.BRS_XYZ.Example.V1;
using Energinet.DataHub.Example.Orchestrations.Tests.Fixtures;
using FluentAssertions;
using NodaTime;
using Xunit.Abstractions;

namespace Energinet.DataHub.Example.Orchestrations.Tests.Integration.Processes.BRS_XYZ.Examples.V1;

/// <summary>
/// Tests that verify that we can start an orchestration instance for the "Example" V1 process.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class Orchestrations_Brs_Xyz_Example_V1_Tests : IAsyncLifetime
{
    public Orchestrations_Brs_Xyz_Example_V1_Tests(ExampleOrchestrationsAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExampleOrchestration_WhenItsStarted_ThenItCompletes()
    {
        // Arrange
        var input = new Input_Brs_Xyz_Example_V1(ExampleTypes.ExampleType1);
        var beforeOrchestrationCreated = SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromSeconds(30));

        // Act
        // Start the orchestration
        var orchestrationId = await StartOrchestrationAsync(
            nameof(Orchestration_Brs_Xyz_Example_V1),
            input);

        orchestrationId.Should().NotBeNull();

        // Assert
        var startedOrchestrationStatus = await Fixture.ExampleOrchestrationsAppManager.DurableClient.WaitForOrchestationStartedAsync(
            createdTimeFrom: beforeOrchestrationCreated.ToDateTimeUtc(),
            name: nameof(Orchestration_Brs_Xyz_Example_V1));
        startedOrchestrationStatus.Should().NotBeNull();

        // => Wait for orchestration to complete
        var completedOrchestrationStatus = await Fixture.ExampleOrchestrationsAppManager.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationId, //startedOrchestrationStatus.InstanceId,
            TimeSpan.FromMinutes(5));
        completedOrchestrationStatus.Should().NotBeNull();
    }

    private async Task<string> StartOrchestrationAsync(string orchestrationName, object input)
    {
        
        var orchestrationId = await Fixture.ExampleOrchestrationsAppManager.DurableClient.StartNewAsync(
            orchestrationName,
            input);

        return orchestrationId;
    }
}
