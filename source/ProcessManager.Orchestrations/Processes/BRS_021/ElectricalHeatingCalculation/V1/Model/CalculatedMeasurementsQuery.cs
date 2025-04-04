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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatementApi;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;

public class CalculatedMeasurementsQuery(DatabricksOptions databricksOptions, Guid orchestrationInstanceId, ILogger logger)
     : QueryBase<CalculatedMeasurementsV1>(databricksOptions, orchestrationInstanceId)
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

    protected override async Task<QueryResult<CalculatedMeasurementsV1>> CreateQueryResultAsync(List<DatabricksSqlRow> currentSet)
    {
        try
        {
            var calculatedMeasurements = new List<CalculatedMeasurement>();

            foreach (var row in currentSet)
            {
                var calculatedMeasurement = CreateCalculatedMeasurement(row);
                calculatedMeasurements.Add(calculatedMeasurement);
            }

            var result = await CreateCalculatedMeasurementsV1Async(calculatedMeasurements).ConfigureAwait(false);
            return QueryResult<CalculatedMeasurementsV1>.Success(result);
        }
        catch (Exception ex)
        {
            var firstRow = currentSet.First();
            var transactionId = firstRow.ToGuid(CalculatedMeasurementsColumnNames.TransactionId);
            var orchestrationType = firstRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.OrchestrationType);
            _logger.LogWarning(ex, $"Creating calculated measurements ({orchestrationType}) failed for orchestration instance id='{OrchestrationInstanceId}', TransactionId='{transactionId}'.");
        }

        return QueryResult<CalculatedMeasurementsV1>.Error();
    }

    protected override bool BelongsToSameSet(DatabricksSqlRow currentRow, DatabricksSqlRow previousRow)
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

    private static CalculatedMeasurement CreateCalculatedMeasurement(DatabricksSqlRow databricksSqlRow)
    {
        return new CalculatedMeasurement(
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.OrchestrationType),
            databricksSqlRow.ToGuid(CalculatedMeasurementsColumnNames.OrchestrationInstanceId),
            databricksSqlRow.ToGuid(CalculatedMeasurementsColumnNames.TransactionId),
            databricksSqlRow.ToInstant(CalculatedMeasurementsColumnNames.TransactionCreationDatetime),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.MeteringPointId),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.MeteringPointType),
            databricksSqlRow.ToInstant(CalculatedMeasurementsColumnNames.ObservationTime),
            databricksSqlRow.ToDecimal(CalculatedMeasurementsColumnNames.Quantity),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.QuantityUnit),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.QuantityQuality),
            databricksSqlRow.ToNonEmptyString(CalculatedMeasurementsColumnNames.Resolution));
    }

    private Task<CalculatedMeasurementsV1> CreateCalculatedMeasurementsV1Async(
        IReadOnlyCollection<CalculatedMeasurement> calculatedMeasurements)
    {
        return Task.FromResult(
            new CalculatedMeasurementsV1(
                MeteringPointId: calculatedMeasurements.First().MeteringPointId,
                MeteringPointType: MeteringPointType.NotUsed,
                RegistrationDateTime: DateTimeOffset.Now,
                StartDateTime: DateTimeOffset.Now,
                EndDateTime: DateTimeOffset.Now,
                ReceiversWithMeteredData: Mapper(calculatedMeasurements)));
    }

    private IReadOnlyCollection<ReceiversWithMeasurementsV1> Mapper(IReadOnlyCollection<CalculatedMeasurement> calculatedMeasurements)
    {
        return calculatedMeasurements.Select(x => new ReceiversWithMeasurementsV1(
            Actors: new List<MarketActorRecipientV1>(),
            Resolution: Resolution.Daily,
            MeasureUnit: MeasurementUnit.KilowattHour,
            StartDateTime: DateTimeOffset.Now,
            EndDateTime: DateTimeOffset.Now,
            Measurements: new List<ReceiversWithMeasurementsV1.AcceptedMeasurements>()))
            .ToList();
    }
}
