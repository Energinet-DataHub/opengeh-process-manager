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

using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Components.Time;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep;

[ParallelWorkflow(WorkflowBucket.Bucket05)]
public class CalculationStepStartJobActivityBrs045MissingMeasurementsLogCalculationV1Tests()
{
    [Fact]
    public async Task Given_CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCalculation_V1_When_RunWithInput_Then_JobIdIsCorrect()
    {
        // Arrange
        var jobRunId = new JobRunId(42);
        var date = Instant.FromUtc(2024, 12, 31, 23, 0, 0);
        var orchestrationInstanceId = new OrchestrationInstanceId(Guid.NewGuid());
        var activityInput =
            new CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCalculation_V1.ActivityInput(
                orchestrationInstanceId);
        var jobParameters = new List<string>
        {
            $"--orchestration-instance-id={activityInput.OrchestrationInstanceId.Value}",
            $"--period-start-datetime={date.PlusDays(-93)}",
            $"--period-end-datetime={date.PlusDays(-3)}",
        };

        var clientMock = new Mock<IDatabricksJobsClient>();
        clientMock
            .Setup(x => x.StartJobAsync("MissingMeasurementsLog", jobParameters))
            .ReturnsAsync(jobRunId);
        var clockMock = new Mock<IClock>();
        clockMock
            .Setup(x => x.GetCurrentInstant())
            .Returns(date);
        var timeHelper = new TimeHelper(DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!);
        var sut = new CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCalculation_V1(clientMock.Object, clockMock.Object, timeHelper);

        // Act
        var actual = await sut.Run(activityInput);

        // Assert
        actual.Should().Be(jobRunId);
    }
}
