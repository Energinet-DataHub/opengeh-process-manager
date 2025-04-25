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

using System.Net;
using System.Reflection;
using System.Text;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.Databricks.SqlStatements;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

/// <summary>
/// A collection of WireMock extensions for mock configuration of
/// Databricks REST API endpoints.
///
/// IMPORTANT developer tips:
///  - It's possible to start the WireMock server in Proxy mode, this means
///    that all requests are proxied to the real URL. And the mappings can be recorded and saved.
///    See https://github.com/WireMock-Net/WireMock.Net/wiki/Proxying
///  - WireMockInspector: https://github.com/WireMock-Net/WireMockInspector/blob/main/README.md
///  - WireMock.Net examples: https://github.com/WireMock-Net/WireMock.Net-examples
/// </summary>
public static class DatabricksSqlStatementApiWireMockExtensions
{
    public const string MockMeteringPointId = "1234567890123456";

    /// <summary>
    /// Setup Databricks SQL statement API mock to be able to respond to a calculated measurements query
    /// </summary>
    public static WireMockServer MockDatabricksCalculatedMeasurementsQueryResponse(
        this WireMockServer server,
        Func<Guid> getOrchestrationInstanceId)
    {
        return server
            .MockDatabricksSqlStatementApi<CalculatedMeasurementsColumnNames>(
                getMockedResponseJson: () => DatabricksCalculatedMeasurementsResultMock(getOrchestrationInstanceId));
    }

    /// <summary>
    /// Setup Databricks SQL statement api mock.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="getMockedResponseJson">Should return a JSON array of values that has the same order as the column name properties in <typeparamref name="TColumnNames"/>.</param>
    /// <typeparam name="TColumnNames">A type only containing string fields symbolizing the name of the mocked columns. See <see cref="CalculatedMeasurementsColumnNames"/> for an example.</typeparam>
    public static WireMockServer MockDatabricksSqlStatementApi<TColumnNames>(
        this WireMockServer server,
        Func<string> getMockedResponseJson)
    {
        // => Databricks SQL Statement API
        var chunkIndex = 0;
        var statementId = Guid.NewGuid().ToString();
        var dataUrlPath = "GetDatabricksDataPath";

        server
            .MockSqlStatements<TColumnNames>(statementId, chunkIndex)
            .MockSqlStatementsResultChunks(statementId, chunkIndex, dataUrlPath)
            .MockSqlStatementsResultStream(dataUrlPath, getMockedResponseJson);

        return server;
    }

    /// <summary>
    /// Mocks the sql/statements POST endpoint, which creates the sql statements in Databricks.
    /// </summary>
    private static WireMockServer MockSqlStatements<TColumnNames>(this WireMockServer server, string statementId, int chunkIndex)
    {
        var request = Request
            .Create()
            .WithPath("/api/2.0/sql/statements")
            .UsingPost();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(DatabricksSqlStatementsResponseMock<TColumnNames>(statementId, chunkIndex));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    /// <summary>
    /// Create a '/api/2.0/sql/statements' JSON response. With a single chunk, containing a single row.
    /// The rest is pretty much dummy data, and can be adjusted as one pleases.
    /// <remarks>
    /// The columns are the properties in the 'TColumnNames' type, which must match (including the order) the columns in Databricks.
    /// </remarks>
    /// </summary>
    private static string DatabricksSqlStatementsResponseMock<TColumnNames>(string statementId, int chunkIndex)
    {
        var json = """
               {
                 "statement_id": "{statementId}",
                 "status": {
                   "state": "SUCCEEDED"
                 },
                 "manifest": {
                   "format": "CSV",
                   "schema": {
                     "column_count": 1,
                     "columns": [
                       {columnArray}
                     ]
                   },
                   "total_chunk_count": 1,
                   "chunks": [
                     {
                       "chunk_index": {chunkIndex},
                       "row_offset": 0,
                       "row_count": 1
                     }
                   ],
                   "total_row_count": 1,
                   "total_byte_count": 293
                 },
                 "result": {
                   "external_links": [
                     {
                       "chunk_index": {chunkIndex},
                       "row_offset": 0,
                       "row_count": 100,
                       "byte_count": 293,
                       "external_link": "https://someplace.cloud-provider.com/very/long/path/...",
                       "expiration": "2023-01-30T22:23:23.140Z"
                     }
                   ]
                 }
               }
               """;

        var columns = string.Join(
            ",",
            GetFieldNames<TColumnNames>()
                .Select(name => $" {{\"name\": \"{name}\" }}"));

        return json.Replace("{statementId}", statementId)
            .Replace("{chunkIndex}", chunkIndex.ToString())
            .Replace(
                "{columnArray}",
                columns);
    }

    /// <summary>
    /// Mocks the sql/statements/{statementId}/result/chunks/{chunkIndex} GET endpoint, which returns an url to get the result.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="statementId"></param>
    /// <param name="chunkIndex"></param>
    /// <param name="dataUrlPath"></param>
    private static WireMockServer MockSqlStatementsResultChunks(this WireMockServer server, string statementId, int chunkIndex, string dataUrlPath)
    {
        var request = Request
            .Create()
            .WithPath($"/api/2.0/sql/statements/{statementId}/result/chunks/{chunkIndex}")
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(DatabricksSqlStatementsExternalLinkResponseMock(chunkIndex, $"{server.Url}/{dataUrlPath}"));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    /// <summary>
    /// Creates a '/api/2.0/sql/statements/{statementId}/result/chunks/{chunkIndex}' JSON response.
    /// Containing a list of 'external_links', which holds information about the rows one are fetching
    /// using the url defined in 'external_link', defined in the elements of 'external_links'.
    /// </summary>
    private static string DatabricksSqlStatementsExternalLinkResponseMock(int chunkIndex, string url)
    {
        var json = """
                   {
                   "external_links": [
                     {
                       "chunk_index": {chunkIndex},
                       "row_offset": 0,
                       "row_count": 1,
                       "byte_count": 246,
                       "external_link": "{url}",
                       "expiration": "2023-01-30T22:23:23.140Z"
                     }
                   ]
                   }
                   """;
        return json.Replace("{chunkIndex}", chunkIndex.ToString())
            .Replace("{url}", url);
    }

    /// <summary>
    /// Mocks the SQL statements result stream, which returns the actual data.
    /// </summary>
    private static WireMockServer MockSqlStatementsResultStream(
        this WireMockServer server,
        string dataUrlPath,
        Func<string> getResultBody)
    {
        var request = Request
            .Create()
            .WithPath($"/{dataUrlPath}")
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithBody(Encoding.UTF8.GetBytes(getResultBody()));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    /// <summary>
    /// Creates a JSON response of a single row in the energy databricks table.
    /// This is the data that is fetched from the 'external_link' defined in the 'DatabricksEnergyStatementExternalLinkResponseMock'.
    /// </summary>
    /// <remarks>
    /// Note that QuantityQualities is a string, containing a list of strings.
    /// </remarks>>
    private static string DatabricksCalculatedMeasurementsResultMock(Func<Guid> getOrchestrationInstanceId)
    {
        // Make sure that the order of the data matches the order of the columns defined in 'CalculatedMeasurementsColumnNames'
        var data = GetFieldNames<CalculatedMeasurementsColumnNames>().Select(columnName => columnName switch
        {
            CalculatedMeasurementsColumnNames.OrchestrationType => "\"???\"",
            CalculatedMeasurementsColumnNames.OrchestrationInstanceId => $"\"{getOrchestrationInstanceId()}\"",
            CalculatedMeasurementsColumnNames.TransactionId => $"\"{Guid.NewGuid()}\"",
            CalculatedMeasurementsColumnNames.TransactionCreationDatetime => "\"2025-04-25T03:00:00.000Z\"",
            CalculatedMeasurementsColumnNames.MeteringPointId => $"\"{MockMeteringPointId}\"",
            CalculatedMeasurementsColumnNames.MeteringPointType => "\"production\"",
            CalculatedMeasurementsColumnNames.ObservationTime => "\"2022-04-25T03:00:00.000Z\"",
            CalculatedMeasurementsColumnNames.Quantity => "\"42.123\"",
            CalculatedMeasurementsColumnNames.QuantityUnit => "\"kWh\"",
            CalculatedMeasurementsColumnNames.QuantityQuality => "\"Calculated\"",
            CalculatedMeasurementsColumnNames.Resolution => "\"PT15M\"",
            _ => throw new ArgumentOutOfRangeException(nameof(columnName), columnName, null),
        }).ToArray();
        var jsonArray = $"""[[{string.Join(",", data)}]]""";
        return jsonArray;
    }

    private static List<string> GetFieldNames<TColumnNames>()
    {
        var fieldInfos = typeof(TColumnNames).GetFields(BindingFlags.Public | BindingFlags.Static);
        return fieldInfos.Select(x => x.GetValue(null)).Cast<string>().ToList();
    }
}
