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
using FluentAssertions;
using CoreDomain = Energinet.DataHub.ProcessManager.Core.Domain;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Unit.Domain.OrchestrationDescription;

public class OrchestrationDescriptionTests
{
    [Fact]
    public void GivenInstance_WhenSettingValidRecurringCronExpression_ThenIsRecurringIsTrue()
    {
        // Arrange
        var sut = new CoreDomain.OrchestrationDescription.OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(
                name: "name",
                version: 1),
            canBeScheduled: true,
            functionName: "FunctionName");

        // Act
        sut.RecurringCronExpression = "0 0 * * *";

        // Assert
        sut.IsRecurring.Should().BeTrue();
    }

    [Fact]
    public void GivenInstance_WhenSettingInvalidRecurringCronExpression_ThenThrowsException()
    {
        // Arrange
        var sut = new CoreDomain.OrchestrationDescription.OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(
                name: "name",
                version: 1),
            canBeScheduled: true,
            functionName: "FunctionName");

        // Act
        var act = () => sut.RecurringCronExpression = "1 2 3 4 5 6";

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
