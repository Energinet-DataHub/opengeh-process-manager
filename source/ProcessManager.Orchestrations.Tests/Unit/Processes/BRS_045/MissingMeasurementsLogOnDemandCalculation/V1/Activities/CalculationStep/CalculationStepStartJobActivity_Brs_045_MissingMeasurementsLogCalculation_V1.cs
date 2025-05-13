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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs;
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Components.Time;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using FluentAssertions;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Activities.CalculationStep;

[ParallelWorkflow(WorkflowBucket.Bucket05)]
public class CalculationStepStartJobActivityBrs045MissingMeasurementsLogCalculationOnDemandV1Tests
{
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;

    [Fact]
    public async Task Given_CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogOnDemandCalculation_V1_When_RunWithInput_ThenJobIdIsCorrect()
    {
        // Arrange
        var jobRunId = new JobRunId(42);
        var gridAreaCodes = new List<string> { "300, 301" };
        var date = Instant.FromUtc(2025, 1, 1, 0, 0, 0);
        var timeHelper = new TimeHelper(_zone);
        var periodStartDate = timeHelper.GetMidnightZonedDateTime(date.Plus(Duration.FromDays(-30)));
        var periodEndDate = timeHelper.GetMidnightZonedDateTime(date.Plus(Duration.FromDays(-9)));
        var orchestrationInstanceId = new OrchestrationInstanceId(Guid.NewGuid());
        var activityInput =
            new CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogOnDemandCalculation_V1.ActivityInput(
                orchestrationInstanceId);
        var jobParameters = new List<string>
        {
            $"--orchestration-instance-id={orchestrationInstanceId.Value}",
            $"--period-start-datetime={periodStartDate}",
            $"--period-end-datetime={periodEndDate}",
            $"--grid-area-codes={string.Join(",", gridAreaCodes)}",
        };

        var clientMock = new Mock<IDatabricksJobsClient>();
        clientMock
            .Setup(x => x.StartJobAsync("MissingMeasurementsLogOnDemand", jobParameters))
            .ReturnsAsync(jobRunId);
        var repositoryMock = new Mock<IOrchestrationInstanceProgressRepository>();
        repositoryMock
            .Setup(x => x.GetAsync(orchestrationInstanceId))
            .Returns(CreateOrchestrationInstance(periodStartDate, periodEndDate, gridAreaCodes));
        var sut = new CalculationStepStartJobActivity_Brs_045_MissingMeasurementsLogOnDemandCalculation_V1(
            repositoryMock.Object,
            clientMock.Object);

        // Act
        var actual = await sut.Run(activityInput);

        // Assert
        actual.Should().Be(jobRunId);
    }

    private Task<OrchestrationInstance> CreateOrchestrationInstance(
        Instant periodsStartDate,
        Instant periodEndDate,
        IReadOnlyCollection<string> gridAreaCodes)
    {
        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            new ActorIdentity(new Actor(ActorNumber.Create("1234567891111"), ActorRole.DataHubAdministrator)),
            new OrchestrationDescription(new OrchestrationDescriptionUniqueName("test", 1), false, "test"),
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            runAt: null,
            idempotencyKey: new IdempotencyKey("123"),
            actorMessageId: new ActorMessageId(Guid.NewGuid().ToString()),
            transactionId: new TransactionId(Guid.NewGuid().ToString()),
            meteringPointId: new MeteringPointId(Guid.NewGuid().ToString()));

        orchestrationInstance.ParameterValue.SetFromInstance(new TestOrchestrationParameter
        {
            PeriodStartDate = periodsStartDate.ToDateTimeOffset(),
            PeriodEndDate = periodEndDate.ToDateTimeOffset(),
            GridAreaCodes = gridAreaCodes,
        });

        return Task.FromResult(orchestrationInstance);
    }

    private class TestOrchestrationParameter : IInputParameterDto
    {
        public DateTimeOffset PeriodStartDate { get; set; }

        public DateTimeOffset PeriodEndDate { get; set; }

        public IReadOnlyCollection<string> GridAreaCodes { get; set; } = [];
    }
}
