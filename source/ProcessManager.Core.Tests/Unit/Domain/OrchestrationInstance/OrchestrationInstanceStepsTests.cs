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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using FluentAssertions;
using NodaTime;
using CoreDomain = Energinet.DataHub.ProcessManager.Core.Domain;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Unit.Domain.OrchestrationInstance;

public class OrchestrationInstanceStepsTests
{
    [Fact]
    public void ExistingStepSequence_WhenGetStep_StepWithSequenceIsReturned()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        var instance = CreateOrchestrationInstance(orchestrationDescription);
        const int stepSequence = 1;

        // Act
        var step = instance.GetStep(stepSequence);

        step.Sequence.Should().Be(stepSequence);
    }

    [Fact]
    public void ExistingStepSequence_WhenTryGetStep_StepWithSequenceIsReturned()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        var instance = CreateOrchestrationInstance(orchestrationDescription);
        const int stepSequence = 1;

        // Act
        var isFound = instance.TryGetStep(stepSequence, out var step);

        isFound.Should().BeTrue();
        step.Should().NotBeNull();
        step!.Sequence.Should().Be(stepSequence);
    }

    [Fact]
    public void UnknownStepSequence_WhenTryGetStep_ReturnsNoStep()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        var instance = CreateOrchestrationInstance(orchestrationDescription);
        const int stepSequence = -1;

        // Act
        var isFound = instance.TryGetStep(stepSequence, out var step);

        isFound.Should().BeFalse();
        step.Should().BeNull();
    }

    [Fact]
    public void UnknownStepSequence_WhenGetStep_ThrowsArgumentOfOfRangeException()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        var instance = CreateOrchestrationInstance(orchestrationDescription);
        const int stepSequence = -1;

        // Act
        instance.Invoking(i => i.GetStep(stepSequence))
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    private static CoreDomain.OrchestrationInstance.OrchestrationInstance CreateOrchestrationInstance(
        CoreDomain.OrchestrationDescription.OrchestrationDescription orchestrationDescription,
        Instant? runAt = default)
    {
        var userIdentity = new UserIdentity(
            new UserId(Guid.NewGuid()),
            new ActorId(Guid.NewGuid()));

        var orchestrationInstance = CoreDomain.OrchestrationInstance.OrchestrationInstance.CreateFromDescription(
            userIdentity,
            orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            runAt: runAt);

        orchestrationInstance.ParameterValue.SetFromInstance(new TestOrchestrationParameter
        {
            TestString = "Test string",
            TestInt = 42,
        });

        return orchestrationInstance;
    }

    private static CoreDomain.OrchestrationDescription.OrchestrationDescription CreateOrchestrationDescription(OrchestrationDescriptionUniqueName? uniqueName = default)
    {
        var orchestrationDescription = new CoreDomain.OrchestrationDescription.OrchestrationDescription(
            uniqueName: uniqueName ?? new OrchestrationDescriptionUniqueName("TestOrchestration", 4),
            canBeScheduled: true,
            functionName: "TestOrchestrationFunction");

        orchestrationDescription.ParameterDefinition.SetFromType<TestOrchestrationParameter>();

        orchestrationDescription.AppendStepDescription("Test step 1");
        orchestrationDescription.AppendStepDescription("Test step 2");
        orchestrationDescription.AppendStepDescription("Test step 3");

        return orchestrationDescription;
    }

    private class TestOrchestrationParameter
    {
        public string? TestString { get; set; }

        public int? TestInt { get; set; }
    }
}
