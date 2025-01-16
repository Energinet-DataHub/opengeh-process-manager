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

using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Models;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Wiremock;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Databricks.Client.Models;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_023_027.V1;

/// <summary>
/// Test to verify the state of an orchestration.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingDurableClient : IAsyncLifetime
{
    private const string CalculationJobName = "CalculatorJob";

    public MonitorOrchestrationUsingDurableClient(
        OrchestrationsAppFixture fixture,
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
                = Fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
        });
        services.AddProcessManagerHttpClients();
        ServiceProvider = services.BuildServiceProvider();

        ProcessManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
    }

    private OrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    private IProcessManagerClient ProcessManagerClient { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        Fixture.OrchestrationsAppManager.EnsureAppHostUsesMockedDatabricksApi(true);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Calculation_WhenRunningAFullOrchestration_HasExceptedHistory()
    {
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());
        var calculationType = CalculationType.WholesaleFixing;

        var orchestrationId = await StartCalculationAsync(
            userIdentity,
            calculationType);

        var completeOrchestrationStatus = await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationId.ToString(),
            TimeSpan.FromSeconds(90));

        var activities = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList();

        activities.Should().NotBeNull().And.Equal(
        [
            new OrchestrationHistoryItem("ExecutionStarted", FunctionName: "Orchestration_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "OrchestrationInitializeActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToRunningActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "CalculationStepStartJobActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "CalculationStepGetJobRunStatusActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToTerminatedActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToRunningActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "EnqueueActorMessagesActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "TransitionStepToTerminatedActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: "OrchestrationTerminateActivity_Brs_023_027_V1"),
            new OrchestrationHistoryItem("ExecutionCompleted"),
        ]);

        // => Verify that the durable function completed successfully
        completeOrchestrationStatus.RuntimeStatus.Should().Be(OrchestrationRuntimeStatus.Completed);

        var serviceBusMessage = await Fixture.ServiceBusEdiBrs023027Receiver
            .ReceiveMessageAsync(TimeSpan.FromSeconds(20), CancellationToken.None);

        serviceBusMessage.Should().NotBeNull();
        var body = Energinet.DataHub.ProcessManager.Abstractions.Contracts.EnqueueActorMessagesV1
            .Parser.ParseJson(serviceBusMessage.Body.ToString())!;

        using var assertionScope = new AssertionScope();
        var calculationCompleted = JsonSerializer.Deserialize<CalculationCompletedV1>(body.Data);
        calculationCompleted!.CalculationType.Should().Be(calculationType);
        calculationCompleted!.OrchestrationInstanceId.Should().Be(orchestrationId);
    }

    [Fact]
    public async Task Calculation_WhenMonitoringDatabricksJobStatusMultipleTimes_ContainsThreeStatusChecksInTheHistory()
    {
        // => Databricks Jobs API
        // The current databricks job state. Can be null, "PENDING", "RUNNING", "TERMINATED" (success)
        // The mock response will wait for the value to not be null before returning
        var jobStatusCallback = new CallbackValue<RunLifeCycleState?>(null);
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            jobStatusCallback.GetValue,
            CalculationJobName);

        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorId: Guid.NewGuid());

        var beforeStartingOrchestration = DateTime.UtcNow.AddSeconds(-5);

        var orchestrationInstanceId = await StartCalculationAsync(userIdentity);

        await Fixture.DurableClient.WaitForOrchestrationRunningAsync(orchestrationInstanceId.ToString());

        jobStatusCallback.SetValue(RunLifeCycleState.PENDING);
        var isPending = await AwaitJobStatusAsync(JobRunStatus.Pending, orchestrationInstanceId);
        isPending.Should().BeTrue("because we expects the orchestration instance to be pending within the given wait time");

        jobStatusCallback.SetValue(RunLifeCycleState.RUNNING);
        var isRunning = await AwaitJobStatusAsync(JobRunStatus.Running, orchestrationInstanceId);
        isRunning.Should().BeTrue("because we expects the orchestration instance to be running within the given wait time");

        jobStatusCallback.SetValue(RunLifeCycleState.TERMINATED);
        var isTerminated = await AwaitJobStatusAsync(JobRunStatus.Completed, orchestrationInstanceId);
        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within the given wait time");

        var status = await Fixture.DurableClient.GetStatusAsync(
            instanceId: orchestrationInstanceId.ToString(),
            showHistory: true,
            showHistoryOutput: true);

        status.History
            .Where(item => item["FunctionName"]?.ToString() == nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1))
            .Should()
            .HaveCount(3, $"because we expects the orchestration instance to have 3 activities of type {nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1)}");
    }

    private async Task<bool> AwaitJobStatusAsync(JobRunStatus expectedStatus, Guid orchestrationInstanceId)
    {
        var matchFound = await Awaiter.TryWaitUntilConditionAsync(
            () => FindOrchestrationActivityAndCheckStatusAsync(
                orchestrationInstanceId,
                nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1),
                expectedStatus),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(500));

        return matchFound;
    }

    private async Task<bool> FindOrchestrationActivityAndCheckStatusAsync(Guid orchestrationInstanceId, string activityName, JobRunStatus runStatus)
    {
        var status = await Fixture.DurableClient.GetStatusAsync(
            instanceId: orchestrationInstanceId.ToString(),
            showHistory: true,
            showHistoryOutput: true);

        var history = status.History
            .OrderByDescending(item => item["Timestamp"]);

        var match = history.FirstOrDefault(
            item =>
                item["FunctionName"]?.ToString() == activityName
                    && Enum.Parse<JobRunStatus>(item["Result"]!.ToString()) == runStatus);

        return match != null;
    }

    private async Task<Guid> StartCalculationAsync(
        UserIdentityDto userIdentity,
        CalculationType calculationType = CalculationType.WholesaleFixing)
    {
        var inputParameter = new CalculationInputV1(
            calculationType,
            GridAreaCodes: new[] { "804" },
            PeriodStartDate: new DateTimeOffset(2023, 1, 31, 23, 0, 0, TimeSpan.Zero),
            PeriodEndDate: new DateTimeOffset(2023, 2, 28, 23, 0, 0, TimeSpan.Zero),
            IsInternalCalculation: false);
        var orchestrationInstanceId = await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartCalculationCommandV1(
                    userIdentity,
                    inputParameter),
                CancellationToken.None);

        return orchestrationInstanceId;
    }
}
