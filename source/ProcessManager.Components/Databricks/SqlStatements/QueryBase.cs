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

namespace Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;

/// <summary>
/// Common base class for querying Databricks.
/// </summary>
public abstract class QueryBase<TResult>(
    DatabricksDatabaseOptions databaseOptions,
    Guid orchestrationInstanceId) :
        IDeltaTableSchemaDescription
            where TResult : IQueryResultDto
{
    /// <inheritdoc/>
    public string DatabaseName => $"{databaseOptions.CatalogName}.{databaseOptions.DatabaseName}";

    /// <inheritdoc/>
    public abstract string DataObjectName { get; }

    /// <inheritdoc/>
    public abstract Dictionary<string, (string DataType, bool IsNullable)> SchemaDefinition { get; }

    public Guid OrchestrationInstanceId { get; } = orchestrationInstanceId;

    internal async IAsyncEnumerable<QueryResult<TResult>> GetAsync(
        DatabricksSqlWarehouseQueryExecutor databricksSqlWarehouseQueryExecutor)
    {
        ArgumentNullException.ThrowIfNull(databricksSqlWarehouseQueryExecutor);

        var statement = DatabricksStatement
            .FromRawSql(BuildSqlQuery())
            .Build();

        DatabricksSqlRow? previousRow = null;
        var currentGroupOfRows = new List<DatabricksSqlRow>();

        await foreach (var currentRow in databricksSqlWarehouseQueryExecutor.ExecuteQueryAsync(statement).ConfigureAwait(false))
        {
            if (previousRow == null || BelongsToSameGroup(currentRow, previousRow))
            {
                currentGroupOfRows.Add(currentRow);
                previousRow = currentRow;
                continue;
            }

            yield return await CreateResultAsync(currentGroupOfRows).ConfigureAwait(false);

            // Next group
            currentGroupOfRows =
            [
                currentRow,
            ];
            previousRow = currentRow;
        }

        // Last group (if any)
        if (currentGroupOfRows.Count != 0)
            yield return await CreateResultAsync(currentGroupOfRows).ConfigureAwait(false);
    }

    protected abstract Task<QueryResult<TResult>> CreateResultAsync(
        IList<DatabricksSqlRow> currentGroupOfRows);

    protected abstract bool BelongsToSameGroup(
        DatabricksSqlRow currentRow,
        DatabricksSqlRow previousRow);

    protected abstract string BuildSqlQuery();
}
