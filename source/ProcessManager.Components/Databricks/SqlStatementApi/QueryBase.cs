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

namespace Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatementApi;

/// <summary>
/// Common base class for querying Databricks.
/// </summary>
public abstract class QueryBase<TResult>(
    DatabricksOptions databricksOptions,
    Guid orchestrationInstanceId)
    : IDeltaTableSchemaDescription
{
    /// <summary>
    /// Name of database to query in.
    /// </summary>
    public string DatabaseName => $"{databricksOptions.CatalogName}.{databricksOptions.DatabaseName}";

    /// <summary>
    /// Name of view or table to query in.
    /// </summary>
    public abstract string DataObjectName { get; }

    /// <summary>
    /// The schema definition of the view expressed as (Column name, Data type, Is nullable).
    ///
    /// Can be used in tests to create a matching data object (e.g. table).
    /// </summary>
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
        var currentSet = new List<DatabricksSqlRow>();

        await foreach (var currentRow in databricksSqlWarehouseQueryExecutor.ExecuteQueryAsync(statement).ConfigureAwait(false))
        {
            if (previousRow == null || BelongsToSameSet(currentRow, previousRow))
            {
                currentSet.Add(currentRow);
                previousRow = currentRow;
                continue;
            }

            yield return await CreateQueryResultAsync(currentSet).ConfigureAwait(false);

            // Next set
            currentSet =
            [
                currentRow,
            ];
            previousRow = currentRow;
        }

        // Last set (if any)
        if (currentSet.Count != 0)
        {
            yield return await CreateQueryResultAsync(currentSet).ConfigureAwait(false);
        }
    }

    protected abstract Task<QueryResult<TResult>> CreateQueryResultAsync(
        List<DatabricksSqlRow> currentSet);

    protected abstract bool BelongsToSameSet(DatabricksSqlRow currentRow, DatabricksSqlRow previousRow);

    protected abstract string BuildSqlQuery();
}
