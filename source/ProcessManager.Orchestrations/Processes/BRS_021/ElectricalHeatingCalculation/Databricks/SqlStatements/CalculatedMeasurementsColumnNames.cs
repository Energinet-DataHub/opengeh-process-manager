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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements;

/// <summary>
/// The column names in the calculated measurements databricks view.
/// </summary>
internal class CalculatedMeasurementsColumnNames
{
    public const string OrchestrationType = "orchestration_type";
    public const string OrchestrationInstanceId = "orchestration_instance_id";
    public const string TransactionId = "transaction_id";
    public const string TransactionCreationDatetime = "transaction_creation_datetime";
    public const string MeteringPointId = "metering_point_id";
    public const string MeteringPointType = "metering_point_type";
    public const string ObservationTime = "observation_time";
    public const string Quantity = "quantity";
    public const string QuantityUnit = "quantity_unit";
    public const string QuantityQuality = "quantity_quality";
    public const string Resolution = "resolution";
}
