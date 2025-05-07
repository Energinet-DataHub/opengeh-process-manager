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
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.Databricks.SqlStatements;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

public class MissingMeasurementsLogQueryFixture : IAsyncLifetime
{
    private readonly DatabricksSchemaManager _databricksSchemaManager;
    private readonly DatabricksQueryOptions _queryOptions;

    public MissingMeasurementsLogQueryFixture()
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

    internal MissingMeasurementsLogQuery CreateSut(Guid orchestrationInstanceId)
    {
        var schemaDescription = new MissingMeasurementsLogSchemaDescription(_queryOptions);

        return new MissingMeasurementsLogQuery(
            Mock.Of<ILogger>(),
            schemaDescription,
            orchestrationInstanceId);
    }

    private static DatabricksSchemaManager CreateDatabricksSchemaManager(
        DatabricksSettings databricksSettings)
    {
        return new DatabricksSchemaManager(
            new DataHub.Core.FunctionApp.TestCommon.Databricks.HttpClientFactory(),
            databricksSettings,
            schemaPrefix: nameof(MissingMeasurementsLogQueryFixture).ToLower());
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
            CatalogName = "hive_metastore",
            DatabaseName = databaseName,
        };
    }

    /// <summary>
    /// Seeding the database as part of a fixture which is reused by multiple tests,
    /// can save time in test setup when using an actual Databricks.
    /// </summary>
    private async Task SeedDatabaseAsync()
    {
        var schemaDescription = new MissingMeasurementsLogSchemaDescription(_queryOptions);

        await _databricksSchemaManager.CreateTableAsync(schemaDescription.DataObjectName, schemaDescription.SchemaDefinition);
        await _databricksSchemaManager.InsertAsync(
            schemaDescription.DataObjectName,
            [
                // First metering point
                [$"'{OrchestrationInstanceId}'", "'190000040000000001'", "'2024-01-14T23:00:00.000'"],
                [$"'{OrchestrationInstanceId}'", "'190000040000000001'", "'2025-01-14T23:00:00.000'"],
                // Second metering point
                [$"'{OrchestrationInstanceId}'", "'190000040000000002'", "'2024-01-29T23:00:00.000'"],
                [$"'{OrchestrationInstanceId}'", "'190000040000000002'", "'2025-01-29T23:00:00.000'"],
            ]);
    }
}
