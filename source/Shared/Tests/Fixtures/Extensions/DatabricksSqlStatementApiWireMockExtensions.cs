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
using System.Text;
using System.Text.Json.Nodes;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

/// <summary>
/// A collection of WireMock extensions for mock configuration of
/// Databricks SQL statement API endpoints (docs: https://docs.databricks.com/api/azure/workspace/statementexecution)
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
    /// <summary>
    /// Setup Databricks SQL statement api mock.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="columnNames">Column names matching the mocked data.</param>
    /// <param name="mockData">The mock data to return.</param>
    /// <param name="columnNameToStringValueConverter">
    /// A method that takes an instance of <typeparamref name="TMockData"/> and a column name, and must return
    /// the string value for the column, for the given input.
    /// See DatabricksSqlStatementApiWireMockTests.ColumnNameToStringValueConverter for an example.
    /// </param>
    /// <typeparam name="TMockData">
    /// The mock data type, which should have properties matching the <paramref name="columnNames"/>.
    /// See ExampleQuery.ExampleQueryData for an example.
    /// </typeparam>
    public static WireMockServer MockDatabricksSqlStatementApi<TMockData>(
        this WireMockServer server,
        IReadOnlyCollection<string> columnNames,
        IReadOnlyCollection<TMockData> mockData,
        Func<TMockData, string, string> columnNameToStringValueConverter)
    {
        // => Databricks SQL Statement API
        const int chunkIndex = 0;
        var statementId = Guid.NewGuid().ToString();
        const string dataUrlPath = "GetDatabricksDataPath";

        server
            .MockSqlStatements(statementId, chunkIndex, columnNames)
            .MockSqlStatementsResultChunks(statementId, chunkIndex, dataUrlPath)
            .MockSqlStatementsResultStream(dataUrlPath, () => GetMockedDataAsJsonBody(columnNames, mockData, columnNameToStringValueConverter));

        return server;
    }

    /// <summary>
    /// Mocks the sql/statements POST endpoint, which creates the sql statements in Databricks.
    /// </summary>
    private static WireMockServer MockSqlStatements(
        this WireMockServer server,
        string statementId,
        int chunkIndex,
        IReadOnlyCollection<string> columnNames)
    {
        var request = Request
            .Create()
            .WithPath("/api/2.0/sql/statements")
            .UsingPost();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(DatabricksSqlStatementsResponseMock(statementId, chunkIndex, columnNames));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    /// <summary>
    /// Create a '/api/2.0/sql/statements' JSON response.
    /// With a single chunk, containing a single row with columns given by <paramref name="columnNames"/>.
    /// The rest is pretty much dummy data, and can be adjusted as one pleases.
    /// </summary>
    private static string DatabricksSqlStatementsResponseMock(
        string statementId,
        int chunkIndex,
        IReadOnlyCollection<string> columnNames)
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
            columnNames
                .Select(name => $" {{\"name\": \"{name}\" }}"));

        return json
            .Replace("{statementId}", statementId)
            .Replace("{chunkIndex}", chunkIndex.ToString())
            .Replace("{columnArray}", columns);
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

    private static string GetMockedDataAsJsonBody<TMockData>(
        IReadOnlyCollection<string> columnNames,
        IReadOnlyCollection<TMockData> mockData,
        Func<TMockData, string, string> columnNameToStringValueConverter)
    {
        var jsonRowValueArray = mockData.Select(
                d =>
                {
                    var columnStringValues = columnNames.Select(
                        columnName => columnNameToStringValueConverter(d, columnName));

                    var columnValuesAsJsonNodes = columnStringValues
                        .Select(v => JsonValue.Create(v))
                        .ToArray<JsonNode>();

                    var columnValuesAsJsonArray = new JsonArray(columnValuesAsJsonNodes);

                    return columnValuesAsJsonArray;
                })
            .ToArray<JsonNode>();

        var jsonArray = new JsonArray(jsonRowValueArray);

        var jsonArrayString = jsonArray.ToJsonString();

        return jsonArrayString;
    }
}
