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

using System.Globalization;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements.Model;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using NodaTime;
using NodaTime.Text;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

public static class DatabricksSqlStatementApiCalculatedMeasurementsExtensions
{
    /// <summary>
    /// Setup Databricks SQL statement API mock to be able to respond to a calculated measurements query
    /// </summary>
    public static WireMockServer MockDatabricksCalculatedMeasurementsQueryResponse(
        this WireMockServer server,
        List<CalculatedMeasurementsRowData> mockData)
    {
        return server
            .MockDatabricksSqlStatementApi<CalculatedMeasurementsColumnNames, CalculatedMeasurementsRowData>(
                mockData,
                ColumnNameToStringValueConverter);
    }

    /// <summary>
    /// This method should map the mock data for all columns names in <see cref="CalculatedMeasurementsColumnNames"/>.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="columnName"></param>
    private static string ColumnNameToStringValueConverter(CalculatedMeasurementsRowData data, string columnName)
    {
        return columnName switch
        {
            CalculatedMeasurementsColumnNames.OrchestrationType => data.OrchestrationType,
            CalculatedMeasurementsColumnNames.OrchestrationInstanceId => data.OrchestrationInstanceId.ToString(),
            CalculatedMeasurementsColumnNames.TransactionId => data.TransactionId.ToString(),
            CalculatedMeasurementsColumnNames.TransactionCreationDatetime => InstantPattern.ExtendedIso.Format(data.TransactionCreationDatetime),
            CalculatedMeasurementsColumnNames.MeteringPointId => data.MeteringPointId,
            CalculatedMeasurementsColumnNames.MeteringPointType => data.MeteringPointType,
            CalculatedMeasurementsColumnNames.ObservationTime => InstantPattern.ExtendedIso.Format(data.ObservationTime),
            CalculatedMeasurementsColumnNames.Quantity => data.Quantity.ToString(CultureInfo.InvariantCulture),
            CalculatedMeasurementsColumnNames.QuantityUnit => data.QuantityUnit,
            CalculatedMeasurementsColumnNames.QuantityQuality => data.QuantityQuality,
            CalculatedMeasurementsColumnNames.Resolution => data.Resolution,
            _ => throw new ArgumentOutOfRangeException(nameof(columnName), columnName, null),
        };
    }

    public record CalculatedMeasurementsRowData(
        Guid OrchestrationInstanceId,
        Guid TransactionId,
        Instant TransactionCreationDatetime,
        string MeteringPointId,
        string MeteringPointType,
        Instant ObservationTime,
        decimal Quantity,
        string OrchestrationType = "???",
        string QuantityUnit = "kWh",
        string QuantityQuality = "Calculated",
        string Resolution = "PT15M");
}
