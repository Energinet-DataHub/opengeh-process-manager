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

using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Databricks.SqlStatements.ExampleQuery;

public class ExampleQuery(
    ILogger logger,
    DatabricksQueryOptions queryOptions,
    Guid orchestrationInstanceId)
        : QueryBase<ExampleQueryRowData>(logger, queryOptions, orchestrationInstanceId)
{
    public override string DataObjectName => "doesn't matter because result is mocked";

    /// <summary>
    /// Must be equal (including the order) to <see cref="ExampleQueryColumnNames"/>.
    /// </summary>
    public override Dictionary<string, (string DataType, bool IsNullable)> SchemaDefinition => new()
    {
        { ExampleQueryColumnNames.Id,    (DeltaTableCommonTypes.String,      false) },
        { ExampleQueryColumnNames.Value, (DeltaTableCommonTypes.Decimal18x3, false) },
    };

    protected override Task<QueryResult<ExampleQueryRowData>> CreateResultFromGroupAsync(IList<DatabricksSqlRow> currentGroupOfRows)
    {
        var row = currentGroupOfRows.Single();

        var exampleRow = new ExampleQueryRowData(
            Id: row.ToGuid(ExampleQueryColumnNames.Id),
            Value: row.ToDecimal(ExampleQueryColumnNames.Value));

        var result = QueryResult<ExampleQueryRowData>.Success(exampleRow);

        return Task.FromResult(result);
    }

    protected override bool BelongsToSameGroup(DatabricksSqlRow currentRow, DatabricksSqlRow previousRow)
    {
        return false;
    }

    protected override string BuildSqlQuery()
    {
        return "doesn't matter because result is mocked";
    }
}
