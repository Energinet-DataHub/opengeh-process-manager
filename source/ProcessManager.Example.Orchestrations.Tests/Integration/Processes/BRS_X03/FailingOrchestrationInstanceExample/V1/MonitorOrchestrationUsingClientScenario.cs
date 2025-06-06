﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03.FailingOrchestrationInstanceExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03.FailingOrchestrationInstanceExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03.FailingOrchestrationInstanceExample.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_X03.FailingOrchestrationInstanceExample.V1;

[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientScenario : IAsyncLifetime
{
    public MonitorOrchestrationUsingClientScenario(
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
        Fixture.SetTestOutputHelper(null);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Tests that we set the step and the orchestration instance to failed if an activity fails (even with retries).
    /// </summary>
    [Fact]
    public async Task Given_FailingOrchestrationInstanceActivity_When_OrchestrationInstanceStarted_Then_OrchestrationInstanceTerminatedWithFailed_AndThen_FailingStepTerminatedWithFailed()
    {
        // Step 1: Start new orchestration instance
        var userIdentity = new UserIdentityDto(Guid.NewGuid(), ActorNumber.Create("1234567891234"), ActorRole.EnergySupplier);
        var startRequestCommand = new StartFailingOrchestrationInstanceExampleCommandV1(userIdentity);

        var orchestrationInstanceId = await Fixture.ProcessManagerClient.StartNewOrchestrationInstanceAsync(
            startRequestCommand,
            CancellationToken.None);

        // Step 2: Query until terminated
        OrchestrationInstanceTypedDto? orchestrationInstance = null;
        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                orchestrationInstance = await Fixture.ProcessManagerClient
                    .GetOrchestrationInstanceByIdAsync(
                        new GetOrchestrationInstanceByIdQuery(
                            userIdentity,
                            orchestrationInstanceId),
                        CancellationToken.None);

                return orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated;
            },
            timeLimit: TimeSpan.FromSeconds(60),
            delay: TimeSpan.FromMilliseconds(200));

        isTerminated.Should().BeTrue("because the orchestration instance should terminate within given wait time");
        orchestrationInstance.Should().NotBeNull();

        using var assertionScope = new AssertionScope();

        orchestrationInstance!.Lifecycle.TerminationState.Should().Be(
            OrchestrationInstanceTerminationState.Failed,
            "because the orchestration instance should be failed");

        orchestrationInstance.Steps.OrderBy(s => s.Sequence).Should().SatisfyRespectively(
            successStep =>
            {
                successStep.Sequence.Should().Be(SuccessStep.StepSequence);
                successStep.Lifecycle.TerminationState.Should().Be(StepInstanceTerminationState.Succeeded);
            },
            failingStep =>
            {
                failingStep.Sequence.Should().Be(FailingStep.StepSequence);
                failingStep.Lifecycle.TerminationState.Should().Be(StepInstanceTerminationState.Failed);
                failingStep.CustomState.Should().Contain(typeof(TaskFailedException).FullName);
                failingStep.CustomState.Should().Contain(FailingActivity_Brs_X03_FailingOrchestrationInstanceExample_V1.ExceptionMessage);
            });
    }
}
