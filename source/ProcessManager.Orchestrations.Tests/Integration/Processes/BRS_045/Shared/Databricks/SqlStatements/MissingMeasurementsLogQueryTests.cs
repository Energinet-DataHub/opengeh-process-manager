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

using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_045.Shared.Databricks.SqlStatements;

public class MissingMeasurementsLogQueryTests : IClassFixture<MissingMeasurementsLogQueryFixture>
{
    private readonly MissingMeasurementsLogQueryFixture _fixture;

    public MissingMeasurementsLogQueryTests(MissingMeasurementsLogQueryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_OrchestrationInstanceIdDoesNotExists_When_GetAsync_Then_QueryResultIsEmpty()
    {
        var sut = _fixture.CreateSut(orchestrationInstanceId: Guid.NewGuid());

        // Act
        var actual = await sut.GetAsync(_fixture.QueryExecutor).ToListAsync();

        Assert.Empty(actual);
    }

    [Fact]
    public async Task Given_OrchestrationInstanceIdExists_When_GetAsync_Then_QueryResultContainsExpectedTransactions()
    {
        var sut = _fixture.CreateSut(_fixture.OrchestrationInstanceId);

        // Act
        var actual = await sut.GetAsync(_fixture.QueryExecutor).ToListAsync();

        Assert.Multiple(
            () => Assert.True(
                actual.Count == 2,
                "Unexpected number of query results"),
            () => Assert.True(
                actual.All(x =>
                    x.IsSuccess),
                "Unexpected status in query result"),
            () => Assert.True(
                actual.All(x =>
                    x.Result != null
                    && x.Result.OrchestrationInstanceId == _fixture.OrchestrationInstanceId),
                "Unexpected id in query result"),
            () => Assert.Single(actual, x =>
                    x.Result != null
                    && x.Result.Dates.Count(m => m.MeteringPointId == "190000040000000001") == 2
                    && x.Result.Dates.Count == 2),
            () => Assert.Single(actual, x =>
                    x.Result != null
                    && x.Result.MissingMeasurementsLogsData.Count(m => m.MeteringPointId == "190000040000000002") == 2
                    && x.Result.MissingMeasurementsLogsData.Count == 2));
    }
}
