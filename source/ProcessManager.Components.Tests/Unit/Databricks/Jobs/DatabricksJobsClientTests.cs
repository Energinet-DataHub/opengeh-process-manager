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

using FluentAssertions;
using Microsoft.Azure.Databricks.Client;
using Microsoft.Azure.Databricks.Client.Models;
using Moq;
using ProcessManager.Components.Databricks.Jobs;
using ProcessManager.Components.Databricks.Jobs.Model;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Databricks.Jobs;

public class DatabricksJobsClientTests
{
    [Theory]
    [ClassData(typeof(StatusTestCases))]
    public async Task EnsureAllRunStatusStatesAndRunTerminationCodesAreHandled(
        RunStatusState runStatusState,
        RunTerminationCode? runTerminationCode)
    {
        // Arrange
        var jobsApiMock = new Mock<IJobsApi>();

        var run = new Run { Status = new RunStatus { State = runStatusState } };
        if (runTerminationCode.HasValue)
            run.Status.TerminationDetails = new TerminationDetails { Code = runTerminationCode.Value };

        jobsApiMock
            .Setup(mock => mock.RunsGet(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((run, null));

        var sut = new DatabricksJobsClient(jobsApiMock.Object);

        // Act
        var act = async () => await sut.GetRunStatusAsync(new JobRunId(1024));

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
