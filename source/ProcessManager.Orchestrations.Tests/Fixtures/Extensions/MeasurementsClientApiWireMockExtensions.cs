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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

public static class MeasurementsClientApiWireMockExtensions
{
    public const string RoutePrefix = "v4/measurements/aggregatedByPeriod";

    public static WireMockServer MockGetAggregatedByPeriodHttpResponse(
        this WireMockServer server,
        string meteringPointId,
        Instant from,
        Instant to)
    {
        var request = Request
            .Create()
            .WithPath(RoutePrefix)
            .WithParam("meteringPointIds", meteringPointId)
            .WithParam("from", from.ToString())
            .WithParam("to", to.ToString())
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(DataForResponse(meteringPointId, from, to));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    private static string DataForResponse(
        string meteringPointId,
        Instant from,
        Instant to)
    {
        return new MeasurementAggregationByPeriodDto(
            MeteringPoint: new MeteringPoint(meteringPointId),
            new Dictionary<string, PointAggregationGroup>()
            {
                {
                    meteringPointId, // What's the key?
                    new PointAggregationGroup(
                        From: from,
                        To: to,
                        Resolution: Resolution.QuarterHourly,
                        PointAggregations: new List<PointAggregation>()
                            {
                                new PointAggregation(
                                From: from,
                                To: to,
                                Quantity: 100m,
                                Quality: Quality.Calculated),
                            })
                },
            }).ToString();
    }

    private static string DataForResponse2(
        string meteringPointId,
        Instant from,
        Instant to)
    {
        if (to - from > Duration.FromDays(1))
            throw new NotImplementedException();

        return new MeasurementAggregationByPeriodDto(
            MeteringPoint: new MeteringPoint(meteringPointId),
            new Dictionary<string, PointAggregationGroup>()
            {
                {
                    meteringPointId, // What's the key?
                    new PointAggregationGroup(
                        From: from,
                        To: to.PlusDays(-1), // Note the plus, is this possible?
                        Resolution: Resolution.QuarterHourly,
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
                    meteringPointId, // What's the key?
                    new PointAggregationGroup(
                        From: to.PlusDays(-1),
                        To: to,
                        Resolution: Resolution.QuarterHourly,
                        PointAggregations: new List<PointAggregation>()
                            {
                                new PointAggregation(
                                From: from,
                                To: to,
                                Quantity: 100m,
                                Quality: Quality.Calculated),
                            })
                },
            }).ToString();
    }
}
