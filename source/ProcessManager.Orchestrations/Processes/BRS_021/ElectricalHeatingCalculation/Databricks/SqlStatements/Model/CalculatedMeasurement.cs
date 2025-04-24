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
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements.Model;

// TODO: Should be a "result" that is "grouped by" the "meta data" (all except: observation_time, quantity, quantity_quality ???)
internal sealed record CalculatedMeasurement(
    string OrchestrationType,
    Guid OrchestrationInstanceId,
    Guid TransactionId,
    Instant TransactionCreationDatetime,
    string MeteringPointId,
    string MeteringPointType,
    string QuantityUnit,
    string Resolution,
    Instant ObservationTime,
    decimal Quantity,
    string QuantityQuality)
        : IQueryResultDto;
