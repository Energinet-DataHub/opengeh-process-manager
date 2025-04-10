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

using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Unit.Domain.OrchestrationInstance;

public class OrchestrationInstanceStepsTests
{
    [Fact]
    public void ExistingStepSequence_WhenGetStep_StepWithSequenceIsReturned()
    {
        var orchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var instance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(orchestrationDescription);
        const int stepSequence = 1;

        // Act
        var step = instance.GetStep(stepSequence);

        step.Sequence.Should().Be(stepSequence);
    }

    [Fact]
    public void ExistingStepSequence_WhenTryGetStep_StepWithSequenceIsReturned()
    {
        var orchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var instance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(orchestrationDescription);
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
        var orchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var instance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(orchestrationDescription);
        const int stepSequence = -1;

        // Act
        var isFound = instance.TryGetStep(stepSequence, out var step);

        isFound.Should().BeFalse();
        step.Should().BeNull();
    }

    [Fact]
    public void UnknownStepSequence_WhenGetStep_ThrowsArgumentOfOfRangeException()
    {
        var orchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();
        var instance = DomainTestDataFactory.CreateUserInitiatedOrchestrationInstance(orchestrationDescription);
        const int stepSequence = -1;

        // Act
        instance.Invoking(i => i.GetStep(stepSequence))
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }
}
