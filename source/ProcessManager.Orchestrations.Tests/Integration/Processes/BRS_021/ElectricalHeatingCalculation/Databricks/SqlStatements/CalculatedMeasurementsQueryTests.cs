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

        // TODO: Move seeding of data and the creation of Sut to the fixture
        await _fixture.DatabricksSchemaManager.CreateTableAsync(sut.DataObjectName, sut.SchemaDefinition);
        await _fixture.DatabricksSchemaManager.InsertAsync(
            sut.DataObjectName,
            [
                // First transaction
                ["'capacity_settlement'", "'48362f74-37e0-4330-b071-b64d0d564b9c'", "'1a0c19a9-8310-5e59-b2e0-d1533927c6b9'", "'2025-04-07T10:04:55.692'", "'190000040000000001'", "'capacity_settlement'", "'2025-01-14T22:00:00.000'", "0.000", "'kWh'", "'calculated'", "'PT1H'"],
                ["'capacity_settlement'", "'48362f74-37e0-4330-b071-b64d0d564b9c'", "'1a0c19a9-8310-5e59-b2e0-d1533927c6b9'", "'2025-04-07T10:04:55.692'", "'190000040000000001'", "'capacity_settlement'", "'2025-01-14T23:00:00.000'", "4.739", "'kWh'", "'calculated'", "'PT1H'"],
                // Second transaction
                ["'capacity_settlement'", "'48362f74-37e0-4330-b071-b64d0d564b9c'", "'1a790ec1-e1d8-51ed-84fd-15d37ad5021a'", "'2025-04-07T10:04:55.692'", "'190000040000000001'", "'capacity_settlement'", "'2025-01-29T22:00:00.000'", "0.000", "'kWh'", "'calculated'", "'PT1H'"],
                ["'capacity_settlement'", "'48362f74-37e0-4330-b071-b64d0d564b9c'", "'1a790ec1-e1d8-51ed-84fd-15d37ad5021a'", "'2025-04-07T10:04:55.692'", "'190000040000000001'", "'capacity_settlement'", "'2025-01-29T23:00:00.000'", "4.739", "'kWh'", "'calculated'", "'PT1H'"],
            ]);
    }
}
