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
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements.Mappers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements.Model;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements;

internal class CalculatedMeasurementsQuery(
    ILogger logger,
    CalculatedMeasurementsSchemaDescription schemaDescription,
    Guid orchestrationInstanceId) :
        QueryBase<CalculatedMeasurement, CalculatedMeasurementsSchemaDescription>(
            logger,
            schemaDescription,
            orchestrationInstanceId)
{
    protected override Task<QueryResult<CalculatedMeasurement>> CreateResultFromGroupAsync(IList<DatabricksSqlRow> groupOfRows)
    {
        var firstRow = groupOfRows.First();

        try
        {
            var measureDataList = new List<MeasureData>();

            foreach (var row in groupOfRows)
            {
                measureDataList.Add(CreateMeasureData(row));
            }

            var result = CreateCalculatedMeasurement(firstRow, measureDataList);
            return Task.FromResult(QueryResult<CalculatedMeasurement>.Success(result));
        }
        catch (Exception ex)
        {
            var transactionId = firstRow.ToGuid(CalculatedMeasurementsColumnNames.TransactionId);
            var orchestrationType = firstRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.OrchestrationType);
            Logger.LogWarning(
                ex,
                "Creating calculated measurements ({OrchestrationType}) failed for orchestration instance id='{OrchestrationInstanceId}', TransactionId='{TransactionId}'.",
                orchestrationType,
                OrchestrationInstanceId,
                transactionId);
        }

        return Task.FromResult(QueryResult<CalculatedMeasurement>.Error());
    }

    protected override bool BelongsToSameGroup(DatabricksSqlRow currentRow, DatabricksSqlRow previousRow)
    {
        return previousRow?.ToGuid(CalculatedMeasurementsColumnNames.TransactionId) == currentRow.ToGuid(CalculatedMeasurementsColumnNames.TransactionId);
    }

    protected override string BuildSqlQuery()
    {
        var columnNames = SchemaDescription.SchemaDefinition.Keys.ToArray();

        return $"""
            SELECT {string.Join(", ", columnNames)}
            FROM {SchemaDescription.DatabaseName}.{SchemaDescription.DataObjectName}
            WHERE {CalculatedMeasurementsColumnNames.OrchestrationInstanceId} = '{OrchestrationInstanceId}'
            ORDER BY {CalculatedMeasurementsColumnNames.TransactionId}, {CalculatedMeasurementsColumnNames.ObservationTime}
            """;
    }

    private static MeasureData CreateMeasureData(DatabricksSqlRow databricksSqlRow)
    {
        return new MeasureData(
            ObservationTime: databricksSqlRow.ToInstant(CalculatedMeasurementsColumnNames.ObservationTime),
            Quantity: databricksSqlRow.ToDecimal(CalculatedMeasurementsColumnNames.Quantity),
            QuantityQuality: databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.QuantityQuality));
    }

    private static CalculatedMeasurement CreateCalculatedMeasurement(DatabricksSqlRow databricksSqlRow, IReadOnlyCollection<MeasureData> measureDataList)
    {
        return new CalculatedMeasurement(
            OrchestrationType: databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.OrchestrationType),
            OrchestrationInstanceId: databricksSqlRow.ToGuid(CalculatedMeasurementsColumnNames.OrchestrationInstanceId),
            TransactionId: databricksSqlRow.ToGuid(CalculatedMeasurementsColumnNames.TransactionId),
            TransactionCreationDatetime: databricksSqlRow.ToInstant(CalculatedMeasurementsColumnNames.TransactionCreationDatetime),
            MeteringPointId: databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.MeteringPointId),
            MeteringPointType: MeteringPointTypeMapper.FromDeltaTableValue(databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.MeteringPointType)),
            QuantityUnit: MeasurementUnitMapper.FromDeltaTableValue(databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.QuantityUnit)),
            Resolution: ResolutionMapper.FromDeltaTableValue(databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.Resolution)),
            MeasureData: measureDataList);
    }
}
