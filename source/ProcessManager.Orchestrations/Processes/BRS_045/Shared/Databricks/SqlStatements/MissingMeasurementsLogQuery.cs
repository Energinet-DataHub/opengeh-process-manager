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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.Databricks.SqlStatements.Model;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.Databricks.SqlStatements;

internal class MissingMeasurementsLogQuery(
    ILogger logger,
    MissingMeasurementsLogSchemaDescription schemaDescription,
    Guid orchestrationInstanceId) :
        QueryBase<MissingMeasurementsLog, MissingMeasurementsLogSchemaDescription>(
            logger,
            schemaDescription,
            orchestrationInstanceId)
{
    protected override Task<QueryResult<MissingMeasurementsLog>> CreateResultFromGroupAsync(IList<DatabricksSqlRow> groupOfRows)
    {
        var firstRow = groupOfRows.First();

        try
        {
            var missingMeasurementsLogDataList = new List<MissingMeasurementsLogData>();

            foreach (var row in groupOfRows)
            {
                missingMeasurementsLogDataList.Add(CreateMissingMeasurementsLogData(row));
            }

            var result = CreateMissingMeasurementsLog(firstRow, missingMeasurementsLogDataList);
            return Task.FromResult(QueryResult<MissingMeasurementsLog>.Success(result));
        }
        catch (Exception ex)
        {
            var orchestrationType = firstRow.ToNonEmptyString(MissingMeasurementsLogColumnNames.OrchestrationType);
            Logger.LogWarning(
                ex,
                "Creating missing measurements log ({OrchestrationType}) failed for orchestration instance id='{OrchestrationInstanceId}'.",
                orchestrationType,
                OrchestrationInstanceId);
        }

        return Task.FromResult(QueryResult<MissingMeasurementsLog>.Error());
    }

    protected override bool BelongsToSameGroup(DatabricksSqlRow currentRow, DatabricksSqlRow previousRow)
    {
        return previousRow.ToGuid(MissingMeasurementsLogColumnNames.GridAreaCode) == currentRow.ToGuid(MissingMeasurementsLogColumnNames.GridAreaCode);
    }

    protected override string BuildSqlQuery()
    {
        return $"""
            SELECT {string.Join(", ", SchemaDescription.Columns)}
            FROM {SchemaDescription.DatabaseName}.{SchemaDescription.DataObjectName}
            WHERE {MissingMeasurementsLogColumnNames.OrchestrationInstanceId} = '{OrchestrationInstanceId}'
            ORDER BY {MissingMeasurementsLogColumnNames.GridAreaCode}, {MissingMeasurementsLogColumnNames.Date}
            """;
    }

    private static MissingMeasurementsLogData CreateMissingMeasurementsLogData(DatabricksSqlRow databricksSqlRow)
    {
        return new MissingMeasurementsLogData(
            MeteringPointId: databricksSqlRow.ToNonEmptyString(MissingMeasurementsLogColumnNames.MeteringPointId),
            Date: databricksSqlRow.ToInstant(MissingMeasurementsLogColumnNames.Date));
    }

    private static MissingMeasurementsLog CreateMissingMeasurementsLog(DatabricksSqlRow databricksSqlRow, IReadOnlyCollection<MissingMeasurementsLogData> missingMeasurementsLogsData)
    {
        return new MissingMeasurementsLog(
            OrchestrationType: databricksSqlRow.ToNonEmptyString(MissingMeasurementsLogColumnNames.OrchestrationType),
            OrchestrationInstanceId: databricksSqlRow.ToGuid(MissingMeasurementsLogColumnNames.OrchestrationInstanceId),
            MissingMeasurementsLogsData: missingMeasurementsLogsData);
    }
}
