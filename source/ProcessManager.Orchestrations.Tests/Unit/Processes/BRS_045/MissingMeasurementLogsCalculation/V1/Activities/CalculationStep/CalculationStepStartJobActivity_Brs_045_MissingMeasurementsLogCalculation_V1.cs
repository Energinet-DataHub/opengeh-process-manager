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
using Energinet.DataHub.ProcessManager.Components.WorkingDays;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using FluentAssertions;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_045.MissingMeasurementLogsCalculation.V1.Activities.CalculationStep;

[ParallelWorkflow(WorkflowBucket.Bucket05)]
public class CalculationStepStartJobActivityBrs045MissingMeasurementsLogCalculationV1Tests()
{
    /// <summary>
    /// Test 1:
    ///           Today
    ///         1st of Jan 202           23rd of Dec. 2024                                            30th of Sep. 2024
    /// ------------|---------------------------|-----------------------------------------------------------|
    ///                 3 working days                                93 calender days
    ///              back in time from today                        back in time from today
    ///
    /// period start: 22nd Dec. 2024 23:00:00
    /// period end: 19th Sep. 2024 23:00:00
    ///
    /// Test 2:
    ///           Today
    ///         7th of May 2025            2nd of May 2025                                            29th of Jan. 2025
    /// ------------|---------------------------|-----------------------------------------------------------|
    ///                 3 working days                                93 calender days
    ///              back in time from today                        back in time from today
    ///
    /// period start: 29th of Jan. 2025 23:00:00
    /// period end: 2nd of May 2025 22:00:00
    /// </summary>
    [Theory]
    [InlineData(2025, 1, 1, 0, 0, 0, -9)]
    [InlineData(2025, 5, 7, 0, 0, 0, -5)]
    public async Task Given_CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCalculation_V1_When_RunWithInput_Then_JobIdIsCorrect(
        int actualYear,
        int actualMonth,
        int actualDay,
        int actualHour,
        int actualMinute,
        int actualSecond,
        int daysBack)
    {
        // Arrange
        var jobRunId = new JobRunId(42);
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;
        var date = Instant.FromUtc(actualYear, actualMonth, actualDay, actualHour, actualMinute, actualSecond);
        var orchestrationInstanceId = new OrchestrationInstanceId(Guid.NewGuid());
        var activityInput =
            new CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCalculation_V1.ActivityInput(
                orchestrationInstanceId);
        var jobParameters = new List<string>
        {
            $"--orchestration-instance-id={orchestrationInstanceId.Value}",
            $"--period-start-datetime={date.Plus(Duration.FromDays(-93)).InZone(zone).Date.AtMidnight().InZoneStrictly(zone).ToInstant()}",
            $"--period-end-datetime={date.Plus(Duration.FromDays(daysBack)).InZone(zone).Date.AtMidnight().InZoneStrictly(zone).ToInstant()}",
        };

        var clientMock = new Mock<IDatabricksJobsClient>();
        clientMock
            .Setup(x => x.StartJobAsync("MissingMeasurementsLog", jobParameters))
            .ReturnsAsync(jobRunId);
        var clockMock = new Mock<IClock>();
        clockMock
            .Setup(x => x.GetCurrentInstant())
            .Returns(date);
        var sut = new CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogCalculation_V1(
            clientMock.Object,
            new DataHubCalendar(clockMock.Object, zone));

        // Act
        var actual = await sut.Run(activityInput);

        // Assert
        actual.Should().Be(jobRunId);
    }
}
