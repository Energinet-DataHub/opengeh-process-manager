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

using System.Globalization;
using Energinet.DataHub.Core.Databricks.SqlStatementExecution;
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Tests.Unit.Databricks.SqlStatements.ExampleQuery;
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
        _mockServer = WireMockServer.Start(port: 1112);
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
    public async Task Given_MockDatabricksSqlStatementApi_When_QueryingCalculatedMeasurements_Then_ReturnsMockedData()
    {
        // Given mocked Databricks SQL statement API
        var mockData = new List<ExampleQueryRowData>
        {
            new(Guid.NewGuid(), 123.45m),
            new(Guid.NewGuid(), 1337.42m),
        };

        _mockServer.MockDatabricksSqlStatementApi<ExampleViewColumnNames, ExampleQueryRowData>(
            mockData,
            ColumnNameToStringValueConverter);

        var schemaDescription = new ExampleViewSchemaDescription(Mock.Of<DatabricksQueryOptions>());
        var query = new ExampleQuery.ExampleQuery(
            logger: Mock.Of<ILogger>(),
            schemaDescription: schemaDescription,
            orchestrationInstanceId: Guid.NewGuid());

        // When querying
        var queryResults = await query.GetAsync(_databricksQueryExecutor)
            .ToListAsync();

        // Then returns mocked data
        Assert.All(
            queryResults,
            result =>
            {
                Assert.Multiple(
                    () => Assert.True(result.IsSuccess),
                    () => Assert.NotNull(result.Result));
            });

        var queryResultsData = queryResults
            .Select(r => r.Result!)
            .ToList();

        Assert.Equal(mockData, queryResultsData);
    }

    private static string ColumnNameToStringValueConverter(ExampleQueryRowData rowData, string columnName)
    {
        return columnName switch
        {
            ExampleViewColumnNames.Id => rowData.Id.ToString(),
            ExampleViewColumnNames.Value => rowData.Value.ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(columnName), columnName, null),
        };
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
