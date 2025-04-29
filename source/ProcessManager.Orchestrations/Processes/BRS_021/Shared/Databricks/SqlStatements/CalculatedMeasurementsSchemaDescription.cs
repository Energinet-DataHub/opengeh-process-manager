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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements;

internal class CalculatedMeasurementsSchemaDescription(
    DatabricksQueryOptions queryOptions) :
        SchemaDescriptionBase(
            queryOptions)
{
    /// <inheritdoc/>
    public override string DataObjectName => "calculated_measurements_v1";

    /// <inheritdoc/>
    public override Dictionary<string, (string DataType, bool IsNullable)> SchemaDefinition => new()
    {
        { CalculatedMeasurementsColumnNames.OrchestrationType,              (DeltaTableCommonTypes.String,      false) },
        { CalculatedMeasurementsColumnNames.OrchestrationInstanceId,        (DeltaTableCommonTypes.String,      false) },
        { CalculatedMeasurementsColumnNames.TransactionId,                  (DeltaTableCommonTypes.String,      false) },
        { CalculatedMeasurementsColumnNames.TransactionCreationDatetime,    (DeltaTableCommonTypes.Timestamp,   false) },
        { CalculatedMeasurementsColumnNames.MeteringPointId,                (DeltaTableCommonTypes.String,      false) },
        { CalculatedMeasurementsColumnNames.MeteringPointType,              (DeltaTableCommonTypes.String,      false) },
        { CalculatedMeasurementsColumnNames.ObservationTime,                (DeltaTableCommonTypes.Timestamp,   false) },
        { CalculatedMeasurementsColumnNames.Quantity,                       (DeltaTableCommonTypes.Decimal18x3, false) },
        { CalculatedMeasurementsColumnNames.QuantityUnit,                   (DeltaTableCommonTypes.String,      false) },
        { CalculatedMeasurementsColumnNames.QuantityQuality,                (DeltaTableCommonTypes.String,      false) },
        { CalculatedMeasurementsColumnNames.Resolution,                     (DeltaTableCommonTypes.String,      false) },
    };
}
