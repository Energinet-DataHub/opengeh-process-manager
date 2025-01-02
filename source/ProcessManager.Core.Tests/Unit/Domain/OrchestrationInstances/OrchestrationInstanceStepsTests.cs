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

using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using FluentAssertions;
using NodaTime;
using OD = Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Unit.Domain.OrchestrationInstances;

public class OrchestrationInstanceStepsTests
{
    [Fact]
    public void KnownInstance_WhenReadFromOrchestrationInstance_SequenceIsEqualToStepInstance()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        var instance = CreateOrchestrationInstance(orchestrationDescription);
        const int stepInstance = 1;

        var step = instance.GetStep(stepInstance);

        step.Sequence.Should().Be(stepInstance);
    }

    [Fact]
    public void KnownInstance_WhenTriedReadFromOrchestrationInstance_SequenceIsEqualToStepInstance()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        var instance = CreateOrchestrationInstance(orchestrationDescription);
        const int stepInstance = 1;

        var isFound = instance.TryGetStep(stepInstance, out var step);

        isFound.Should().BeTrue();
        step.Should().NotBeNull();
        step!.Sequence.Should().Be(stepInstance);
    }

    [Fact]
    public void UnknownInstance_WhenTriedRead_ReturnsNoInstance()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        var instance = CreateOrchestrationInstance(orchestrationDescription);
        const int stepInstance = -1;

        var isFound = instance.TryGetStep(stepInstance, out var step);

        isFound.Should().BeFalse();
        step.Should().BeNull();
    }

    [Fact]
    public void UnknownInstance_WhenRead_ThrowsArgumentOfOfRangeException()
    {
        var orchestrationDescription = CreateOrchestrationDescription();
        var instance = CreateOrchestrationInstance(orchestrationDescription);
        const int stepInstance = -1;

        instance.Invoking(i => i.GetStep(stepInstance))
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    private static OrchestrationInstance CreateOrchestrationInstance(
        OD.OrchestrationDescription orchestrationDescription,
        Instant? runAt = default)
    {
        var userIdentity = new UserIdentity(
            new UserId(Guid.NewGuid()),
            new ActorId(Guid.NewGuid()));

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
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

    private static ProcessManagement.Core.Domain.OrchestrationDescription.OrchestrationDescription CreateOrchestrationDescription(OrchestrationDescriptionUniqueName? uniqueName = default)
    {
        var orchestrationDescription = new OD.OrchestrationDescription(
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
