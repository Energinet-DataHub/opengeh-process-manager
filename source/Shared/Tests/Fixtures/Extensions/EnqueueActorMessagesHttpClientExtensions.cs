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
using WireMock.Matchers;
using WireMock.Matchers.Request;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

/// <summary>
/// Extensions for setting up the WireMock server to mock the http status code response.
/// </summary>
public static class EnqueueActorMessagesHttpWireMockExtensions
{
    public const string RoutePrefix = "/api/enqueue";

    public static WireMockServer MockEnqueueActorMessagesHttpResponse(this WireMockServer server, string routeName, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var request = Request
            .Create()
            .WithPath($"{RoutePrefix}/{routeName}")
            .UsingPost();

        var response = Response
            .Create()
            .WithStatusCode(statusCode)
            .WithHeader(HeaderNames.ContentType, "application/json");

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    /// <summary>
    /// Check the amount of times the mocked endpoint was called.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="routeName">The route name to check against</param>
    /// <returns>Returns the amount of time the endpoint was called.</returns>
    public static int CountEnqueueActorMessagesHttpMockCalls(this WireMockServer server, string routeName)
    {
        var logEntries = server
            .FindLogEntries(
                new RequestMessagePathMatcher(
                    MatchBehaviour.AcceptOnMatch,
                    MatchOperator.And,
                    $"{RoutePrefix}/{routeName}"));

        return logEntries.Count;
    }
}
