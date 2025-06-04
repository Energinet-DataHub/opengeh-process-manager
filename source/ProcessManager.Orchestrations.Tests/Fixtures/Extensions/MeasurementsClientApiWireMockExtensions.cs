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
    public const string RoutePrefix = "/v5/measurements/aggregatedByPeriod";

    public static WireMockServer MockGetAggregatedByYearForPeriodHttpResponse(
        this WireMockServer server,
        string meteringPointId,
        Instant from,
        Instant to)
    {
        var request = Request
            .Create()
            .WithPath(RoutePrefix)
            .WithParam("MeteringPointIds", meteringPointId)
            .WithParam("Aggregation", Aggregation.Year.ToString())
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
        var data = """
                   {
                     "MeasurementAggregations": [
                     {
                         "MeteringPoint": {
                             "Id": "{meteringPointId}"
                         },
                         "PointAggregationGroups": {
                   """;
        data = data.Replace("{meteringPointId}", meteringPointId);
        data += WritePointAggregationGroups(meteringPointId, from, to);
        data += "}}]}";

        return data;
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
                    key: meteringPointId + (from.Year() + i) + Resolution.Yearly,
                    from: fromInLoop,
                    to: endOfPeriod));

            fromInLoop = endOfPeriod.PlusHours(1);
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

    private static Instant GetEndOfPeriod(Instant startOfPeriod, Instant possibleEnd)
    {
        var endOfYear = InstantPattern.General.Parse(startOfPeriod.Year() + "-12-31T23:00:00Z").Value;

        if (endOfYear < possibleEnd)
            return endOfYear;

        return possibleEnd;
    }
}
