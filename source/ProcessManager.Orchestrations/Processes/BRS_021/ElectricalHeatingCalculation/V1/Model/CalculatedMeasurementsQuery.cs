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
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatementApi;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;

public class CalculatedMeasurementsQuery(DatabricksOptions databricksOptions, Guid orchestrationInstanceId) : CalculationResultQueryBase<CalculatedMeasurementsMessageDto>(databricksOptions, orchestrationInstanceId)
{
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

    protected override Task<QueryResult<CalculatedMeasurementsMessageDto>> CreateResultAsync(List<DatabricksSqlRow> currentResultSet)
    {
        return Task.FromResult(QueryResult<List<DatabricksSqlRow>>.Success(new List<DatabricksSqlRow>()))
            .ConfigureAwait(false);
    }

    protected override bool BelongsToSameResultSet(DatabricksSqlRow currentResult, DatabricksSqlRow previousResult)
    {
        return previousResult?.ToGuid(CalculatedMeasurementsColumnNames.TransactionId) == currentResult.ToGuid(CalculatedMeasurementsColumnNames.TransactionId);
    }

    protected override string BuildSqlQuery()
    {
        var columnNames = SchemaDefinition.Keys.ToArray();

        return $"""
                SELECT {string.Join(", ", columnNames)}
                FROM {DatabaseName}.{DataObjectName}
                WHERE {CalculatedMeasurementsColumnNames.OrchestrationInstanceId} = '{OrchestrationInstanceId}'
                ORDER BY {CalculatedMeasurementsColumnNames.MeteringPointId}, {CalculatedMeasurementsColumnNames.ObservationTime}
                """;
    }
}
