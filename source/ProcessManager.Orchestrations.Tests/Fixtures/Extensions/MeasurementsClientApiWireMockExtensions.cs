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

        if (numberOfGroupingsByYear == 1)
        {
            var response = new MeasurementAggregationByPeriodDto(
                MeteringPoint: new MeteringPoint(meteringPointId),
                new Dictionary<string, PointAggregationGroup>()
                {
                    {
                        meteringPointId + from.Year() + Resolution.Yearly, // What's the key?
                        new PointAggregationGroup(
                            From: from,
                            To: to.PlusDays(-1), // Note the plus, is this possible?
                            Resolution: Resolution.Yearly,
                            PointAggregations: new List<PointAggregation>()
                                {
                                    new PointAggregation(
                                    From: from,
                                    To: to,
                                    Quantity: 100m,
                                    Quality: Quality.Calculated),
                                })
                    },
                    {
                        meteringPointId + to.PlusDays(-1).Year() + Resolution.Yearly, // What's the key?
                        new PointAggregationGroup(
                            From: to.PlusDays(-1),
                            To: to,
                            Resolution: Resolution.Hourly,
                            PointAggregations: new List<PointAggregation>()
                                {
                                    new PointAggregation(
                                    From: from,
                                    To: to,
                                    Quantity: 100m,
                                    Quality: Quality.Calculated),
                                })
                    },
                });

            return JsonSerializer.Serialize(response);
        }

        var dictionary = new Dictionary<string, PointAggregationGroup>();
        var fromInLoop = from;

        for (int i = 0; i < numberOfGroupingsByYear; i++)
        {
            var endOfPeriod = GetEndOfPeriod(fromInLoop, to);

            dictionary.Add(
                meteringPointId + fromInLoop.Year() + Resolution.Yearly,
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

        var result = "{MeasurementAggregations:" + JsonSerializer.Serialize(responseWithMultipleYears) + "}";
        return result;
    }

    private static Instant GetEndOfPeriod(Instant from, Instant to)
    {
        var endOfYear = InstantPattern.General.Parse(from.Year() + "-12-31T23:00:00Z").Value;

        if (endOfYear < to)
            return endOfYear;

        return to;
    }
}
