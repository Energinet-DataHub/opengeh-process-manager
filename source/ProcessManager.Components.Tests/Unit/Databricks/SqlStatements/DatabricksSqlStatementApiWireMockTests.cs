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

using Energinet.DataHub.Core.Databricks.SqlStatementExecution;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WireMock.Server;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Databricks.SqlStatements;

public class DatabricksSqlStatementApiWireMockTests : IAsyncLifetime
{
    private readonly WireMockServer _mockServer;
    private readonly DatabricksSqlWarehouseQueryExecutor _databricksQueryExecutor;

    public DatabricksSqlStatementApiWireMockTests()
    {
        _mockServer = WireMockServer.Start(port: 1111);
        _databricksQueryExecutor = CreateDatabricksExecutor(_mockServer.Url!);
    }

    public Task InitializeAsync()
    {
        _mockServer.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _mockServer.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_MockedDatabricks_When_QueryingCalculatedMeasurements_Then_ReturnsMockedData()
    {
        var orchestrationInstanceId = Guid.NewGuid();

        _mockServer.MockCalculatedMeasurementsQueryResponse(
            getOrchestrationInstanceId: () => orchestrationInstanceId);

        var query = new CalculatedMeasurementsQuery(
            logger: Mock.Of<ILogger>(),
            databricksOptions: Mock.Of<DatabricksQueryOptions>(),
            orchestrationInstanceId: orchestrationInstanceId);

        var queryResults = await query.GetAsync(_databricksQueryExecutor)
            .ToListAsync();

        var queryResult = Assert.Single(queryResults);

        Assert.Multiple(
            () => Assert.True(queryResult.IsSuccess),
            () => Assert.NotNull(queryResult.Result));

        Assert.Equal(orchestrationInstanceId, queryResult.Result!.OrchestrationInstanceId);
    }

    private static DatabricksSqlWarehouseQueryExecutor CreateDatabricksExecutor(
        string mockUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorkspaceUrl"] = mockUrl,
                ["WarehouseId"] = "dummy",
                ["WorkspaceToken"] = "dummy",
            })
            .Build();

        var serviceProvider = new ServiceCollection()
            .AddDatabricksSqlStatementExecution(configuration)
            .BuildServiceProvider();

        return serviceProvider.GetRequiredService<DatabricksSqlWarehouseQueryExecutor>();
    }
}
