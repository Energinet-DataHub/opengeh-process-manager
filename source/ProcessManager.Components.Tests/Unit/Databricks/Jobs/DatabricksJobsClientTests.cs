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
using FluentAssertions;
using Microsoft.Azure.Databricks.Client;
using Microsoft.Azure.Databricks.Client.Models;
using Moq;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Databricks.Jobs;

public class DatabricksJobsClientTests
{
    public DatabricksJobsClientTests()
    {
        JobRunId = new JobRunId(1024);
        JobsApiMock = new Mock<IJobsApi>();
        Sut = new DatabricksJobsClient(JobsApiMock.Object);
    }

    public JobRunId JobRunId { get; }

    public Mock<IJobsApi> JobsApiMock { get; }

    internal DatabricksJobsClient Sut { get; }

    [Theory]
    // When RunStatus.State is not Terminated or Terminating, RunStatus.State will determine JobRunState
    [InlineData(JobRunStatus.Pending, RunStatusState.PENDING, null)]
    [InlineData(JobRunStatus.Queued, RunStatusState.QUEUED, null)]
    [InlineData(JobRunStatus.Running, RunStatusState.RUNNING, null)]
    [InlineData(JobRunStatus.Failed, RunStatusState.BLOCKED, null)]
    // When RunStatus.State is Terminated or Terminating, TerminationDetails.Code will determine JobState
    // Terminated
    [InlineData(JobRunStatus.Completed, RunStatusState.TERMINATED, RunTerminationCode.SUCCESS)]
    [InlineData(JobRunStatus.Canceled, RunStatusState.TERMINATED, RunTerminationCode.USER_CANCELED)]
    [InlineData(JobRunStatus.Canceled, RunStatusState.TERMINATED, RunTerminationCode.CANCELED)]
    [InlineData(JobRunStatus.Failed, RunStatusState.TERMINATED, RunTerminationCode.RUN_EXECUTION_ERROR)]
    // Terminating
    [InlineData(JobRunStatus.Completed, RunStatusState.TERMINATING, RunTerminationCode.SUCCESS)]
    [InlineData(JobRunStatus.Canceled, RunStatusState.TERMINATING, RunTerminationCode.USER_CANCELED)]
    [InlineData(JobRunStatus.Canceled, RunStatusState.TERMINATING, RunTerminationCode.CANCELED)]
    [InlineData(JobRunStatus.Failed, RunStatusState.TERMINATING, RunTerminationCode.RUN_EXECUTION_ERROR)]
    public async Task GetRunStatusAsync_WhenCalledWithKnownCombinations_ReturnsExpectedJobRunState(
        JobRunStatus expectedJobRunState,
        RunStatusState runStatusState,
        RunTerminationCode? runTerminationCode)
    {
        // Arrange
        var run = new Run { Status = new RunStatus { State = runStatusState } };
        if (runTerminationCode.HasValue)
            run.Status.TerminationDetails = new TerminationDetails { Code = runTerminationCode.Value };

        JobsApiMock
            .Setup(mock => mock.RunsGet(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((run, null));

        // Act
        var actualJobRunStatus = await Sut.GetJobRunStatusAsync(JobRunId);

        // Assert
        actualJobRunStatus.Should().Be(expectedJobRunState);
    }

    [Theory]
    [ClassData(typeof(StatusTestCases))]
    public async Task GetRunStatusAsync_WhenCalledWithAllCombinations_NoExceptionsAreThrown(
        RunStatusState runStatusState,
        RunTerminationCode? runTerminationCode)
    {
        // Arrange
        var run = new Run { Status = new RunStatus { State = runStatusState } };
        if (runTerminationCode.HasValue)
            run.Status.TerminationDetails = new TerminationDetails { Code = runTerminationCode.Value };

        JobsApiMock
            .Setup(mock => mock.RunsGet(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((run, null));

        // Act
        var act = async () => await Sut.GetJobRunStatusAsync(JobRunId);

        // Assert
        await act.Should().NotThrowAsync("all states should be handled");
    }

    private sealed class StatusTestCases : TheoryData<RunStatusState, RunTerminationCode?>
    {
        public StatusTestCases()
        {
            foreach (var runStatusState in Enum.GetValues<RunStatusState>())
            {
                if (runStatusState is RunStatusState.TERMINATED or RunStatusState.TERMINATING)
                {
                    foreach (var runTerminationCode in Enum.GetValues<RunTerminationCode>())
                    {
                        Add(runStatusState, runTerminationCode);
                    }
                }
                else
                {
                    Add(runStatusState, null);
                }
            }
        }
    }
}
