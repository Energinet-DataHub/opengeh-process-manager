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
using System.Text.Json;
using Energinet.DataHub.Measurements.Abstractions.Api.Models;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Microsoft.Net.Http.Headers;
using NodaTime;
using NodaTime.Text;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

public static class MeasurementsClientApiWireMockExtensions
{
    public const string RoutePrefix = "/v4/measurements/aggregatedByPeriod";

    public static WireMockServer MockGetAggregatedByYearForPeriodHttpResponse(
        this WireMockServer server,
        string meteringPointId,
        Instant from,
        Instant to)
    {
        var request = Request
            .Create()
            .WithPath(RoutePrefix)
            // .WithParam("meteringPointIds", meteringPointId)
            // .WithParam("from", from.ToString())
            // .WithParam("to", to.ToString())
            // .WithParam("Aggregation", Aggregation.Year.ToString())
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(ResponseForYearlyAggregation(meteringPointId, from, to));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    private static string ResponseForYearlyAggregation(
        string meteringPointId,
        Instant from,
        Instant to)
    {
        var numberOfGroupingsByYear = to.Year() - from.Year() + 1;

        var dictionary = new Dictionary<string, PointAggregationGroup>();
        var fromInLoop = from;

        for (int i = 0; i < numberOfGroupingsByYear; i++)
        {
            var endOfPeriod = GetEndOfPeriod(fromInLoop, to);

            dictionary.Add(
                meteringPointId + (from.Year() + i) + Resolution.Yearly,
                new PointAggregationGroup(
                    From: fromInLoop,
                    To: endOfPeriod,
                    Resolution: Resolution.Yearly,
                    PointAggregations: new List<PointAggregation>()
                    {
                        new PointAggregation(
                            From: fromInLoop,
                            To: endOfPeriod,
                            Quantity: 100m + i,
                            Quality: Quality.Calculated),
                    }));
            fromInLoop = endOfPeriod;
        }

        var responseWithMultipleYears = new MeasurementAggregationByPeriodDto(
            MeteringPoint: new MeteringPoint(meteringPointId),
            dictionary);

        var hej123 =
            "{ \"MeasurementAggregations\": ["
            + "   { \"MeteringPoint\" :"
            + "   {"
            + "     \"Id\" : \"123456789012345678\""
            + "   },"
            + "   \"PointAggregationGroups\" : {";

        hej123 += WritePointAggregationGroups(meteringPointId, from, to);
        hej123 += "}}]}";

        var result = "{ \"MeasurementAggregations\": [" + JsonSerializer.Serialize(responseWithMultipleYears) + "]}";
        return hej123;
    }

    private static string WritePointAggregationGroups(string meteringPointId, Instant from, Instant to)
    {
        var aggregations = new List<string>();
        var numberOfGroupingsByYear = to.Year() - from.Year() + 1;
        var fromInLoop = from;

        for (int i = 0; i < numberOfGroupingsByYear; i++)
        {
            var endOfPeriod = GetEndOfPeriod(fromInLoop, to);

            aggregations.Add(
                WritePointAggregationGroup(
                    meteringPointId + (from.Year() + i) + Resolution.Yearly,
                    fromInLoop,
                    endOfPeriod));
        }

        return string.Join(",", aggregations);
    }

    private static string WritePointAggregationGroup(string key, Instant from, Instant to)
    {
        // "Resolution" : 4 == Resolution.Yearly
        // "Quality" : 2 == Quality.Calculated
        var stringToAlter = """
               "{key}" : {
                 "From" : "{from}",
                 "To" : "{to}",
                 "Resolution" : 4,
                 "PointAggregations" : [ {
                   "From" : "{from}",
                   "To" : "{to}",
                   "Quantity" : 100,
                   "Quality" : 2
                 } ]
               }
               """;

        return stringToAlter
            .Replace("{key}", key)
            .Replace("{from}", from.ToString())
            .Replace("{to}", to.ToString());
    }

    private static Instant GetEndOfPeriod(Instant from, Instant to)
    {
        var endOfYear = InstantPattern.General.Parse(from.Year() + "-12-31T23:00:00Z").Value;

        if (endOfYear < to)
            return endOfYear;

        return to;
    }
}
