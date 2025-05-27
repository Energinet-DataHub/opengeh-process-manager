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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Model;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_X01.InputExample.V1;

[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class MonitorOrchestrationUsingDurableClient : IAsyncLifetime
{
    private readonly UserIdentityDto _userIdentity = new(
        UserId: Guid.NewGuid(),
        ActorNumber: ActorNumber.Create("1234567890123"),
        ActorRole: ActorRole.EnergySupplier);

    public MonitorOrchestrationUsingDurableClient(
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

    [Fact]
    public async Task ExampleOrchestration_WhenRunningEveryActivity_HasExpectedHistory()
    {
        // Start new orchestration instance
        var input = new InputV1(
            ShouldSkipSkippableStep: false);
        var orchestrationInstanceId = await Fixture.ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartInputExampleCommandV1(
                    _userIdentity,
                    input),
                CancellationToken.None);

        // => Wait for completion
        var completeOrchestrationStatus = await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationInstanceId.ToString(),
            TimeSpan.FromSeconds(20));

        // => Expect history
        using var assertionScope = new AssertionScope();

        var activities = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList();

        activities.Should().NotBeNull().And.Equal(
        [
            new OrchestrationHistoryItem("ExecutionStarted", FunctionName: "Orchestration_Brs_X01_InputExample_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionOrchestrationToRunningActivity_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "GetOrchestrationInstanceContextActivity_Brs_X01_InputExample_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToRunningActivity_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToTerminatedActivity_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToRunningActivity_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToTerminatedActivity_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionOrchestrationToTerminatedActivity_V1"),
            new OrchestrationHistoryItem("ExecutionCompleted"),
        ]);

        // => Verify that the durable function completed successfully
        var last = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Last();
        last.Value<string>("EventType").Should().Be("ExecutionCompleted");
        last.Value<string>("Result").Should().Be("Success");
    }

    [Fact]
    public async Task ExampleOrchestration_WhenSkippingActivities_HasExpectedHistory()
    {
        // Start new orchestration instance
        var input = new InputV1(
            ShouldSkipSkippableStep: true);
        var orchestrationInstanceId = await Fixture.ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartInputExampleCommandV1(
                    _userIdentity,
                    input),
                CancellationToken.None);

        // => Wait for completion
        var completeOrchestrationStatus = await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationInstanceId.ToString(),
            TimeSpan.FromSeconds(20));

        // => Expect history
        using var assertionScope = new AssertionScope();

        var activities = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList();

        activities.Should().NotBeNull().And.Equal(
        [
            new OrchestrationHistoryItem("ExecutionStarted", FunctionName: "Orchestration_Brs_X01_InputExample_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionOrchestrationToRunningActivity_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "GetOrchestrationInstanceContextActivity_Brs_X01_InputExample_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToRunningActivity_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToTerminatedActivity_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionOrchestrationToTerminatedActivity_V1"),
            new OrchestrationHistoryItem("ExecutionCompleted"),
        ]);

        // => Verify that the durable function completed successfully
        var last = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Last();
        last.Value<string>("EventType").Should().Be("ExecutionCompleted");
        last.Value<string>("Result").Should().Be("Success");
    }
}
