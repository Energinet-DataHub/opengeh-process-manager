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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.Databricks.SqlStatements;

internal class MissingMeasurementsLogQuery(
    ILogger logger,
    MissingMeasurementsLogSchemaDescription schemaDescription,
    Guid orchestrationInstanceId) :
        QueryBase<Model.MissingMeasurementsLog, MissingMeasurementsLogSchemaDescription>(
            logger,
            schemaDescription,
            orchestrationInstanceId)
{
    private const string OrchestrationType = "missing_measurements_log";

    protected override Task<QueryResult<Model.MissingMeasurementsLog>> CreateResultFromGroupAsync(IList<DatabricksSqlRow> groupOfRows)
    {
        var firstRow = groupOfRows.First();

        try
        {
            var dates = groupOfRows.Select(row => row.ToInstant(MissingMeasurementsLogColumnNames.Date)).ToList();
            var result = CreateMissingMeasurementsLog(firstRow, dates);
            return Task.FromResult(QueryResult<Model.MissingMeasurementsLog>.Success(result));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Creating missing measurements log ({OrchestrationType}) failed for orchestration instance id='{OrchestrationInstanceId}'.",
                OrchestrationType,
                OrchestrationInstanceId);
        }

        return Task.FromResult(QueryResult<Model.MissingMeasurementsLog>.Error());
    }

    protected override bool BelongsToSameGroup(DatabricksSqlRow currentRow, DatabricksSqlRow previousRow)
    {
        return previousRow.ToNonEmptyString(MissingMeasurementsLogColumnNames.MeteringPointId) == currentRow.ToNonEmptyString(MissingMeasurementsLogColumnNames.MeteringPointId);
    }

    protected override string BuildSqlQuery()
    {
        return $"""
            SELECT {string.Join(", ", SchemaDescription.Columns)}
            FROM {SchemaDescription.DatabaseName}.{SchemaDescription.DataObjectName}
            WHERE {MissingMeasurementsLogColumnNames.OrchestrationInstanceId} = '{OrchestrationInstanceId}'
            ORDER BY {MissingMeasurementsLogColumnNames.MeteringPointId}, {MissingMeasurementsLogColumnNames.Date}
            """;
    }

    private Model.MissingMeasurementsLog CreateMissingMeasurementsLog(DatabricksSqlRow databricksSqlRow, IReadOnlyCollection<Instant> dates)
    {
        return new Model.MissingMeasurementsLog(
            OrchestrationInstanceId: databricksSqlRow.ToGuid(MissingMeasurementsLogColumnNames.OrchestrationInstanceId),
            MeteringPointId: databricksSqlRow.ToNonEmptyString(MissingMeasurementsLogColumnNames.MeteringPointId),
            Dates: dates);
    }
}
