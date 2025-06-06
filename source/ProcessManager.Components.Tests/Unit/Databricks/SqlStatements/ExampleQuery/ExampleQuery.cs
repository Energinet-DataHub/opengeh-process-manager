﻿// Copyright 2020 Energinet DataHub A/S
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
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Databricks.SqlStatements.ExampleQuery;

internal class ExampleQuery(
    ILogger logger,
    ExampleViewSchemaDescription schemaDescription,
    Guid orchestrationInstanceId)
        : QueryBase<ExampleQueryRowData, ExampleViewSchemaDescription>(
            logger,
            schemaDescription,
            orchestrationInstanceId)
{
    protected override Task<QueryResult<ExampleQueryRowData>> CreateResultFromGroupAsync(IList<DatabricksSqlRow> currentGroupOfRows)
    {
        var row = currentGroupOfRows.Single();

        var rowData = new ExampleQueryRowData(
            Id: row.ToGuid(ExampleViewColumnNames.Id),
            Value: row.ToDecimal(ExampleViewColumnNames.Value));

        var rowDataResult = QueryResult<ExampleQueryRowData>.Success(rowData);

        return Task.FromResult(rowDataResult);
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
