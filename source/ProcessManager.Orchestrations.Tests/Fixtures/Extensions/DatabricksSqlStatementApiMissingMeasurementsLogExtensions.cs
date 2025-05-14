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

using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Moq;
using NodaTime;
using NodaTime.Text;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

public static class DatabricksSqlStatementApiMissingMeasurementsLogExtensions
{
    /// <summary>
    /// Setup Databricks SQL statement API mock to be able to respond to a missing measurements log query.
    /// </summary>
    public static WireMockServer MockDatabricksMissingMeasurementsLogQueryResponse(
        this WireMockServer server,
        List<MissingMeasurementsLogRowData> mockData)
    {
        var schemaDescription = new MissingMeasurementsLogSchemaDescription(Mock.Of<DatabricksQueryOptions>());

        return server
            .MockDatabricksSqlStatementApi(
                schemaDescription.Columns,
                mockData,
                ColumnNameToStringValueConverter);
    }

    /// <summary>
    /// This method should map the mock data for all columns names in <see cref="MissingMeasurementsLogSchemaDescription"/>.
    /// </summary>
    private static string ColumnNameToStringValueConverter(MissingMeasurementsLogRowData data, string columnName)
    {
        return columnName switch
        {
            MissingMeasurementsLogColumnNames.OrchestrationInstanceId => data.OrchestrationInstanceId.ToString(),
            MissingMeasurementsLogColumnNames.MeteringPointId => data.MeteringPointId,
            MissingMeasurementsLogColumnNames.Date => InstantPattern.ExtendedIso.Format(data.Date),
            _ => throw new ArgumentOutOfRangeException(nameof(columnName), columnName, null),
        };
    }

    public record MissingMeasurementsLogRowData(
        Guid OrchestrationInstanceId,
        string MeteringPointId,
        Instant Date);
}
