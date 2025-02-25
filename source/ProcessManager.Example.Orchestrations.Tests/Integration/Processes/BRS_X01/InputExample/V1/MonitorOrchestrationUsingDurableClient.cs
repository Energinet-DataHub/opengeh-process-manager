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
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Shared.Tests.Models;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_X01.InputExample.V1;

[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class MonitorOrchestrationUsingDurableClient : IAsyncLifetime
{
    private readonly UserIdentityDto _userIdentity = new UserIdentityDto(
        UserId: Guid.NewGuid(),
        ActorNumber: ActorNumber.Create("1234567890123"),
        ActorRole: ActorRole.EnergySupplier);

    public MonitorOrchestrationUsingDurableClient(
        ExampleOrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                = Fixture.ProcessManagerAppManager.ApplicationIdUri,
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

    [Fact]
    public async Task ExampleOrchestration_WhenRunningEveryActivity_HasExpectedHistory()
    {
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Start new orchestration instance
        var input = new InputV1(
            ShouldSkipSkippableStep: false);
        var orchestrationInstanceId = await processManagerClient
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
        var processManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();

        // Start new orchestration instance
        var input = new InputV1(
            ShouldSkipSkippableStep: true);
        var orchestrationInstanceId = await processManagerClient
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
