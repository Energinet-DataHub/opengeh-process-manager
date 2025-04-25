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
using Energinet.DataHub.Core.FunctionApp.TestCommon.Databricks;
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

public class CalculatedMeasurementsQueryFixture : IAsyncLifetime
{
    private readonly DatabricksSchemaManager _databricksSchemaManager;
    private readonly DatabricksQueryOptions _queryOptions;

    public CalculatedMeasurementsQueryFixture()
    {
        var integrationTestConfiguration = new IntegrationTestConfiguration();

        _databricksSchemaManager = CreateDatabricksSchemaManager(integrationTestConfiguration.DatabricksSettings);
        _queryOptions = CreateQueryOptions(_databricksSchemaManager.SchemaName);

        QueryExecutor = CreateDatabricksExecutor(integrationTestConfiguration.DatabricksSettings);
        OrchestrationInstanceId = Guid.NewGuid();
    }

    public DatabricksSqlWarehouseQueryExecutor QueryExecutor { get; }

    public Guid OrchestrationInstanceId { get; }

    public async Task InitializeAsync()
    {
        await _databricksSchemaManager.CreateSchemaAsync();
        await SeedDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _databricksSchemaManager.DropSchemaAsync();
    }

    internal CalculatedMeasurementsQuery CreateSut(Guid orchestrationInstanceId)
    {
        return new CalculatedMeasurementsQuery(
            Mock.Of<ILogger>(),
            _queryOptions,
            orchestrationInstanceId);
    }

    private static DatabricksSchemaManager CreateDatabricksSchemaManager(
        DatabricksSettings databricksSettings)
    {
        return new DatabricksSchemaManager(
            new DataHub.Core.FunctionApp.TestCommon.Databricks.HttpClientFactory(),
            databricksSettings,
            schemaPrefix: nameof(CalculatedMeasurementsQueryFixture).ToLower());
    }

    private static DatabricksSqlWarehouseQueryExecutor CreateDatabricksExecutor(
    DatabricksSettings databricksSettings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorkspaceUrl"] = databricksSettings.WorkspaceUrl,
                ["WarehouseId"] = databricksSettings.WarehouseId,
                ["WorkspaceToken"] = databricksSettings.WorkspaceAccessToken,
            })
            .Build();

        var serviceProvider = new ServiceCollection()
            .AddDatabricksSqlStatementExecution(configuration)
            .BuildServiceProvider();

        return serviceProvider.GetService<DatabricksSqlWarehouseQueryExecutor>()!;
    }

    private static DatabricksQueryOptions CreateQueryOptions(string databaseName)
    {
        return new DatabricksQueryOptions
        {
            DatabaseName = databaseName,
            CatalogName = "hive_metastore",
        };
    }

    /// <summary>
    /// Seeding the database as part of a fixture which is reused by multiple tests,
    /// can save time in test setup when using an actual Databricks.
    /// </summary>
    private async Task SeedDatabaseAsync()
    {
        // It's necessary to create the query to be able to get the data object name and schema definition.
        var dummyQuery = new CalculatedMeasurementsQuery(
            Mock.Of<ILogger>(),
            _queryOptions,
            orchestrationInstanceId: Guid.NewGuid());

        await _databricksSchemaManager.CreateTableAsync(dummyQuery.DataObjectName, dummyQuery.SchemaDefinition);
        await _databricksSchemaManager.InsertAsync(
            dummyQuery.DataObjectName,
            [
                // First transaction
                ["'capacity_settlement'", $"'{OrchestrationInstanceId}'", "'1a0c19a9-8310-5e59-b2e0-d1533927c6b9'", "'2025-04-07T10:04:55.692'", "'190000040000000001'", "'capacity_settlement'", "'2025-01-14T22:00:00.000'", "0.000", "'kWh'", "'calculated'", "'PT1H'"],
                ["'capacity_settlement'", $"'{OrchestrationInstanceId}'", "'1a0c19a9-8310-5e59-b2e0-d1533927c6b9'", "'2025-04-07T10:04:55.692'", "'190000040000000001'", "'capacity_settlement'", "'2025-01-14T23:00:00.000'", "4.739", "'kWh'", "'calculated'", "'PT1H'"],
                // Second transaction
                ["'capacity_settlement'", $"'{OrchestrationInstanceId}'", "'1a790ec1-e1d8-51ed-84fd-15d37ad5021a'", "'2025-04-07T10:04:55.692'", "'190000040000000001'", "'capacity_settlement'", "'2025-01-29T22:00:00.000'", "0.000", "'kWh'", "'calculated'", "'PT1H'"],
                ["'capacity_settlement'", $"'{OrchestrationInstanceId}'", "'1a790ec1-e1d8-51ed-84fd-15d37ad5021a'", "'2025-04-07T10:04:55.692'", "'190000040000000001'", "'capacity_settlement'", "'2025-01-29T23:00:00.000'", "4.739", "'kWh'", "'calculated'", "'PT1H'"],
            ]);
    }
}
