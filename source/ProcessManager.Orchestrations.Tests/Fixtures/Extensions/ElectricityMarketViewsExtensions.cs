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
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

/// <summary>
/// A collection of extensions methods that provides abstractions on top of
/// the more technical databricks api extensions in <see cref="DatabricksApiWireMockExtensions"/>
/// </summary>
public static class ElectricityMarketViewsExtensions
{
    public static WireMockServer MockGetGridOwner(this WireMockServer server, string gridAreaCode)
    {
        // using var request = new HttpRequestMessage(HttpMethod.Post, "api/get-grid-area-owner?gridAreaCode=" + gridAreaCode);
        var request = Request
            .Create()
            .WithPath("/api/get-grid-area-owner")
            .WithParam("gridAreaCode", gridAreaCode)
            .UsingPost();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(BuildJobsListJson(gridAreaCode));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    /// <summary>
    /// Creates a '/jobs/list' JSON response with exactly one job
    /// containing the given job id and job name.
    /// </summary>
    private static string BuildJobsListJson(string gridAreaCode)
    {
        var json = """
                   {
                     "GridAccessProviderGln": "{gridAreaCode}"
                   }
                   """;
        return json
            .Replace("{gridAreaCode}", gridAreaCode);
    }
}
