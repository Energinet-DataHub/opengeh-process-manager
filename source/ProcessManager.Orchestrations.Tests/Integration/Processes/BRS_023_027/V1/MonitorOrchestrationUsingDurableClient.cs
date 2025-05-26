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
using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.EnqueActorMessagesStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Wiremock;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Energinet.DataHub.ProcessManager.Shared.Tests.Model;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Databricks.Client.Models;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Proto = Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Contracts;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_023_027.V1;

/// <summary>
/// Test to verify the state of an orchestration.
/// </summary>
[ParallelWorkflow(WorkflowBucket.Bucket01)]
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
        services.AddTokenCredentialProvider();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            // Process Manager HTTP client
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"]
                = SubsystemAuthenticationOptionsForTests.ApplicationIdUri,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = Fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),

            // Process Manager message client
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.StartTopicName)}"]
                = Fixture.OrchestrationsAppManager.ProcessManagerStartTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.NotifyTopicName)}"]
                = Fixture.ProcessManagerAppManager.ProcessManagerNotifyTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataStartTopicName)}"]
                = Fixture.OrchestrationsAppManager.Brs021ForwardMeteredDataStartTopic.Name,
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataNotifyTopicName)}"]
                = Fixture.OrchestrationsAppManager.Brs021ForwardMeteredDataNotifyTopic.Name,
        });
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(Fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerHttpClients();
        services.AddProcessManagerMessageClient();
        ServiceProvider = services.BuildServiceProvider();

        ProcessManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
        ProcessManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
    }

    private OrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    private IProcessManagerClient ProcessManagerClient { get; }

    private IProcessManagerMessageClient ProcessManagerMessageClient { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        Fixture.OrchestrationsAppManager.EnsureAppHostUsesMockedDatabricksApi(true);

        Fixture.EnqueueBrs023027ServiceBusListener.ResetMessageHandlersAndReceivedMessages();
        Fixture.IntegrationEventServiceBusListener.ResetMessageHandlersAndReceivedMessages();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Asserting that we can run a full orchestration, asserting the service bus message sent to EDI and the
    /// message published to the shared integration event topic.
    /// Lastly we assert that the history of the orchestration is as expected.
    /// </summary>
    [Fact]
    public async Task Calculation_WhenRunningAFullOrchestration_HasExpectedHistoryAndServiceBusMessages()
    {
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        // step 1.0: Start the orchestration
        var orchestrationId = await StartCalculationAsync(
            calculationType: CalculationType.WholesaleFixing);

        // step 2.0: Wait for service bus message to EDI and mock a response
        await Fixture.EnqueueBrs023027ServiceBusListener.WaitAndMockServiceBusMessageToAndFromEdi(
            processManagerMessageClient: ProcessManagerMessageClient,
            orchestrationInstanceId: orchestrationId);

        // step 2.5: Wait for the integration event to be published
        await Fixture.IntegrationEventServiceBusListener.WaitAndAssertCalculationEnqueueCompletedIntegrationEvent(
            orchestrationInstanceId: orchestrationId,
            calculationType: Proto.CalculationType.WholesaleFixing);

        var completeOrchestrationStatus = await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationId.ToString(),
            TimeSpan.FromSeconds(30));

        // step 3.0: Verify the history of the orchestration
        var activities = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList();

        activities.Should().NotBeNull().And.Equal(
        [
            new OrchestrationHistoryItem("ExecutionStarted", FunctionName: nameof(Orchestration_Brs_023_027_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionOrchestrationToRunningActivity_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(GetOrchestrationInstanceContextActivity_Brs_023_027_V1)),

            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionStepToRunningActivity_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(CalculationStepStartJobActivity_Brs_023_027_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionStepToTerminatedActivity_V1)),

            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionStepToRunningActivity_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(EnqueueActorMessagesActivity_Brs_023_027_V1)),
            new OrchestrationHistoryItem("TimerCreated"),
            new OrchestrationHistoryItem("EventRaised",   Name: CalculationEnqueueActorMessagesCompletedNotifyEventV1.OrchestrationInstanceEventName),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionStepToTerminatedActivity_V1)),

            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(PublishCalculationEnqueueCompletedActivity_brs_023_027_V1)),

            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionOrchestrationToTerminatedActivity_V1)),
            new OrchestrationHistoryItem("ExecutionCompleted"),
        ]);

        // => Verify that the durable function completed successfully
        completeOrchestrationStatus.RuntimeStatus.Should().Be(OrchestrationRuntimeStatus.Completed);
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

        var orchestrationInstanceId = await StartCalculationAsync();

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

        // Lets the durable function run to completion
        await Fixture.EnqueueBrs023027ServiceBusListener.WaitAndMockServiceBusMessageToAndFromEdi(
            processManagerMessageClient: ProcessManagerMessageClient,
            orchestrationInstanceId: orchestrationInstanceId);

        var status = await Fixture.DurableClient.GetStatusAsync(
            instanceId: orchestrationInstanceId.ToString(),
            showHistory: true,
            showHistoryOutput: true);

        status.History
            .Where(item => item["FunctionName"]?.ToString() == nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1))
            .Should()
            .HaveCount(3, $"because we expects the orchestration instance to have 3 activities of type {nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1)}");
    }

    /// <summary>
    /// Asserting that no subsystem is informed about the calculation completed when running an internal calculation.
    /// Asserting that the orchestration terminated successfully.
    /// </summary>
    [Fact]
    public async Task Calculation_WhenStartingAnInternalCalculation_NoSubsystemIsInformedAndTerminatesSuccessfully()
    {
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        // step 1.0: Start the orchestration
        var orchestrationId = await StartCalculationAsync(
            calculationType: CalculationType.Aggregation,
            isInternalCalculation: true);

        var completeOrchestrationStatus = await Fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationId.ToString(),
            TimeSpan.FromSeconds(30));

        // step 3.0: Verify the history of the orchestration
        var activities = completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList();

        activities.Should().NotBeNull().And.Equal(
        [
            new OrchestrationHistoryItem("ExecutionStarted", FunctionName: nameof(Orchestration_Brs_023_027_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionOrchestrationToRunningActivity_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(GetOrchestrationInstanceContextActivity_Brs_023_027_V1)),

            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionStepToRunningActivity_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(CalculationStepStartJobActivity_Brs_023_027_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(CalculationStepGetJobRunStatusActivity_Brs_023_027_V1)),
            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionStepToTerminatedActivity_V1)),

            new OrchestrationHistoryItem("TaskCompleted", FunctionName: nameof(TransitionOrchestrationToTerminatedActivity_V1)),
            new OrchestrationHistoryItem("ExecutionCompleted"),
        ]);

        // => Verify that the durable function completed successfully
        completeOrchestrationStatus.RuntimeStatus.Should().Be(OrchestrationRuntimeStatus.Completed);

        var isTerminatedSuccessfully = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationInstance = await ProcessManagerClient
                    .GetOrchestrationInstanceByIdAsync<CalculationInputV1>(
                        new GetOrchestrationInstanceByIdQuery(
                            Fixture.DefaultUserIdentity,
                            orchestrationId),
                        CancellationToken.None);

                return
                    orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                    && orchestrationInstance.Lifecycle.TerminationState == OrchestrationInstanceTerminationState.Succeeded;
            },
            timeLimit: TimeSpan.FromSeconds(20),
            delay: TimeSpan.FromSeconds(3));

        isTerminatedSuccessfully.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }

    /// <summary>
    /// Asserts that if EDI fails in enqueue messages, then:
    /// The step is marked as failed.
    /// No integration event is published.
    /// And the orchestration is marked as failed.
    /// </summary>
    [Fact]
    public async Task Calculation_WhenMessageEnqueueFails_NoIntegrationEventIsPublishedAndHasStatusFailed()
    {
        Fixture.OrchestrationsAppManager.MockServer.MockDatabricksJobStatusResponse(
            RunLifeCycleState.TERMINATED,
            CalculationJobName);

        // step 1.0: Start the orchestration
        var orchestrationId = await StartCalculationAsync(
            calculationType: CalculationType.WholesaleFixing);

        // step 2.0: Wait for service bus message to EDI and mock a failed response
        await Fixture.EnqueueBrs023027ServiceBusListener.WaitAndMockServiceBusMessageToAndFromEdi(
            processManagerMessageClient: ProcessManagerMessageClient,
            orchestrationInstanceId: orchestrationId,
            successfulResponse: false);

        await AwaitOrchestrationRuntimeStatusAsync(orchestrationId.ToString(), OrchestrationRuntimeStatus.Failed);

        var completeOrchestrationStatus = await Fixture.DurableClient.GetStatusAsync(
            instanceId: orchestrationId.ToString(),
            showHistory: true);

        using var assertionScope = new AssertionScope();

        // step 3.0: Verify the function state and history
        // => Verify that the history does not contain publish integration event activity
        completeOrchestrationStatus.History
            .OrderBy(item => item["Timestamp"])
            .Select(item => item.ToObject<OrchestrationHistoryItem>())
            .ToList()
            .Should()
            .NotContain(historyItem => historyItem!.FunctionName == nameof(PublishCalculationEnqueueCompletedActivity_brs_023_027_V1));

        // => Verify that the durable function failed
        completeOrchestrationStatus.RuntimeStatus.Should().Be(OrchestrationRuntimeStatus.Failed);

        var orchestrationInstance = await ProcessManagerClient
            .GetOrchestrationInstanceByIdAsync<CalculationInputV1>(
                new GetOrchestrationInstanceByIdQuery(
                    Fixture.DefaultUserIdentity,
                    orchestrationId),
                CancellationToken.None);

        orchestrationInstance.Lifecycle.TerminationState.Should().Be(OrchestrationInstanceTerminationState.Failed);
        orchestrationInstance.Steps
            .Single(step => step.Sequence == EnqueueActorMessagesStep.EnqueueActorMessagesStepSequence)
            .Lifecycle.TerminationState
            .Should().Be(StepInstanceTerminationState.Failed);
    }

    private async Task AwaitOrchestrationRuntimeStatusAsync(string orchestrationInstanceId, OrchestrationRuntimeStatus status)
    {
        await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                var orchestrationStatus = await Fixture.DurableClient.GetStatusAsync(orchestrationInstanceId);
                return orchestrationStatus.RuntimeStatus == status;
            },
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(1));
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
        UserIdentityDto? userIdentity = null,
        CalculationType calculationType = CalculationType.WholesaleFixing,
        bool isInternalCalculation = false)
    {
        var inputParameter = new CalculationInputV1(
            calculationType,
            GridAreaCodes: ["804"],
            PeriodStartDate: new DateTimeOffset(2023, 1, 31, 23, 0, 0, TimeSpan.Zero),
            PeriodEndDate: new DateTimeOffset(2023, 2, 28, 23, 0, 0, TimeSpan.Zero),
            IsInternalCalculation: isInternalCalculation);
        var orchestrationInstanceId = await ProcessManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartCalculationCommandV1(
                    userIdentity ?? Fixture.DefaultUserIdentity,
                    inputParameter),
                CancellationToken.None);

        return orchestrationInstanceId;
    }
}
