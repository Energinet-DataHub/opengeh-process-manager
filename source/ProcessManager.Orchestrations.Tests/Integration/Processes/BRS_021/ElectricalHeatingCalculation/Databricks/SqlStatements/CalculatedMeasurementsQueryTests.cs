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

using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements;

public class CalculatedMeasurementsQueryTests : IClassFixture<CalculatedMeasurementsQueryFixture>
{
    private readonly CalculatedMeasurementsQueryFixture _fixture;

    public CalculatedMeasurementsQueryTests(CalculatedMeasurementsQueryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_X_When_Y_Then_Z()
    {
        var loggerStub = Mock.Of<ILogger>();
        var orchestrationInstanceId = Guid.NewGuid();

        var sut = new CalculatedMeasurementsQuery(
            loggerStub,
            _fixture.QueryOptions,
            orchestrationInstanceId);

        await _fixture.DatabricksSchemaManager.CreateTableAsync(sut.DataObjectName, sut.SchemaDefinition);
        ////await _fixture.DatabricksSchemaManager.InsertAsync(
        ////    sut.DataObjectName,
        ////    [
        ////        ["'61d60f89-bbc5-4f7a-be98-6139aab1c1b2'", "'balance_fixing'", "'2023-02-01 23:00:00.000000'", "'2023-02-12 23:00:00.000000'", "'111'", "'10e4e982-91dc-4e1c-9079-514ed45a64a8'", "'543'", "'5790001662234'", "'7080000729821'", "'production'", "NULL", "'PT1H'", "'2023-02-01 23:00:00.000000'", "'39471.336'", "'kWh'", "Array('measured')"],
        ////        ["'61d60f89-bbc5-4f7a-be98-6139aab1c1b2'", "'balance_fixing'", "'2023-02-01 23:00:00.000000'", "'2023-02-12 23:00:00.000000'", "'111'", "'10e4e982-91dc-4e1c-9079-514ed45a64a8'", "'543'", "'5790001662234'", "'7080000729821'", "'production'", "NULL", "'PT1H'", "'2023-02-02 00:00:00.000000'", "'39472.336'", "'kWh'", "Array('measured')"],
        ////        ["'61d60f89-bbc5-4f7a-be98-6139aab1c1b2'", "'balance_fixing'", "'2023-02-01 23:00:00.000000'", "'2023-02-12 23:00:00.000000'", "'111'", "'10e4e982-91dc-4e1c-9079-514ed45a64a8'", "'543'", "'5790001662234'", "'7080000729821'", "'production'", "NULL", "'PT1H'", "'2023-02-02 05:00:00.000000'", "'39473.336'", "'kWh'", "Array('measured')"],
        ////    ]);
    }
}
