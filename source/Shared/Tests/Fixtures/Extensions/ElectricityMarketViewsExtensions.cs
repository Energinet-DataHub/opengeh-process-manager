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
using Microsoft.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

/// <summary>
/// Extensions for setting up the WireMock server to mock the Electricity Market Views API
/// </summary>
public static class ElectricityMarketViewsExtensions
{
    public static WireMockServer MockElectricityMarketHealthCheck(this WireMockServer server)
    {
        var request = Request
            .Create()
            .WithPath("/api/monitor/live")
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody("{\"status\":\"Healthy\",\"totalDuration\":\"00:00:00.0028802\",\"entries\":{\"self\":{\"data\":{},\"description\":\"Version: 1.0.0 PR: 131 SHA: 0608532d7fb306928c61a5ec422a5fabad172c22\",\"duration\":\"00:00:00.0011102\",\"status\":\"Healthy\",\"tags\":[]}}");

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    public static WireMockServer MockGetGridAreaOwner(this WireMockServer server, string gridAreaCode)
    {
        var request = Request
            .Create()
            .WithPath("/api/get-grid-area-owner")
            .WithParam("gridAreaCode", gridAreaCode)
            .UsingPost();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(BuildGridAreaOwnerDtoJson(gridAreaCode));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    /// <summary>
    /// Creates a payload for a GridAreaOwnerDto
    /// </summary>
    private static string BuildGridAreaOwnerDtoJson(string gridAreaCode)
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
