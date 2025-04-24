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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements.Model;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements;

internal class CalculatedMeasurementsQuery(
    DatabricksQueryOptions databricksOptions,
    Guid orchestrationInstanceId,
    ILogger<CalculatedMeasurementsQuery> logger) :
        QueryBase<CalculatedMeasurement>(
            databricksOptions,
            orchestrationInstanceId)
{
    private readonly ILogger _logger = logger;

    public override string DataObjectName => "calculated_measurements_v1";

    public override Dictionary<string, (string DataType, bool IsNullable)> SchemaDefinition => new()
    {
        { CalculatedMeasurementsColumnNames.OrchestrationType, (DeltaTableCommonTypes.String, false) },
        { CalculatedMeasurementsColumnNames.OrchestrationInstanceId, (DeltaTableCommonTypes.String, false) },
        { CalculatedMeasurementsColumnNames.TransactionId, (DeltaTableCommonTypes.Timestamp, false) },
        { CalculatedMeasurementsColumnNames.TransactionCreationDatetime, (DeltaTableCommonTypes.Timestamp, false) },
        { CalculatedMeasurementsColumnNames.MeteringPointId, (DeltaTableCommonTypes.BigInt, false) },
        { CalculatedMeasurementsColumnNames.MeteringPointType, (DeltaTableCommonTypes.String, false) },
        { CalculatedMeasurementsColumnNames.ObservationTime, (DeltaTableCommonTypes.String, false) },
        { CalculatedMeasurementsColumnNames.Quantity, (DeltaTableCommonTypes.String, false) },
        { CalculatedMeasurementsColumnNames.QuantityUnit, (DeltaTableCommonTypes.String, false) },
        { CalculatedMeasurementsColumnNames.QuantityQuality, (DeltaTableCommonTypes.String, false) },
        { CalculatedMeasurementsColumnNames.Resolution, (DeltaTableCommonTypes.Timestamp, false) },
    };

    protected override Task<QueryResult<CalculatedMeasurement>> CreateResultAsync(IList<DatabricksSqlRow> groupOfRows)
    {
        try
        {
            var calculatedMeasurements = new List<CalculatedMeasurement>();

            foreach (var row in groupOfRows)
            {
                var calculatedMeasurement = CreateCalculatedMeasurement(row);
                calculatedMeasurements.Add(calculatedMeasurement);
            }

            // TODO: Here we should "groupd by" common properties and create the result based on that
            var result = calculatedMeasurements.First();
            return Task.FromResult(QueryResult<CalculatedMeasurement>.Success(result));
        }
        catch (Exception ex)
        {
            var firstRow = groupOfRows.First();
            var transactionId = firstRow.ToGuid(CalculatedMeasurementsColumnNames.TransactionId);
            var orchestrationType = firstRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.OrchestrationType);
            _logger.LogWarning(ex, $"Creating calculated measurements ({orchestrationType}) failed for orchestration instance id='{OrchestrationInstanceId}', TransactionId='{transactionId}'.");
        }

        return Task.FromResult(QueryResult<CalculatedMeasurement>.Error());
    }

    protected override bool BelongsToSameGroup(DatabricksSqlRow currentRow, DatabricksSqlRow previousRow)
    {
        return previousRow?.ToGuid(CalculatedMeasurementsColumnNames.TransactionId) == currentRow.ToGuid(CalculatedMeasurementsColumnNames.TransactionId);
    }

    protected override string BuildSqlQuery()
    {
        var columnNames = SchemaDefinition.Keys.ToArray();

        return $"""
            SELECT {string.Join(", ", columnNames)}
            FROM {DatabaseName}.{DataObjectName}
            WHERE {CalculatedMeasurementsColumnNames.OrchestrationInstanceId} = '{OrchestrationInstanceId}'
            ORDER BY {CalculatedMeasurementsColumnNames.TransactionId}, {CalculatedMeasurementsColumnNames.ObservationTime}
            """;
    }

    // TODO: Use mappers to map as soon as possible
    private static CalculatedMeasurement CreateCalculatedMeasurement(DatabricksSqlRow databricksSqlRow)
    {
        return new CalculatedMeasurement(
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.OrchestrationType),
            databricksSqlRow.ToGuid(CalculatedMeasurementsColumnNames.OrchestrationInstanceId),
            databricksSqlRow.ToGuid(CalculatedMeasurementsColumnNames.TransactionId),
            databricksSqlRow.ToInstant(CalculatedMeasurementsColumnNames.TransactionCreationDatetime),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.MeteringPointId),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.MeteringPointType),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.QuantityUnit),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.Resolution),
            databricksSqlRow.ToInstant(CalculatedMeasurementsColumnNames.ObservationTime),
            databricksSqlRow.ToDecimal(CalculatedMeasurementsColumnNames.Quantity),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.QuantityQuality));
    }
}
