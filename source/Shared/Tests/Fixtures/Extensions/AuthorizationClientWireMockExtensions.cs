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
using Energinet.DataHub.MarketParticipant.Authorization.Model;
using Microsoft.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

public static class AuthorizationClientWireMockExtensions
{
    public static WireMockServer MockGetAuthorizedPeriodsAsync(
        this WireMockServer server,
        int numberOfPeriods = 1,
        string? meteringPointId = null)
    {
        var request = Request
            .Create()
            .UsingPost();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(CreateSignature(numberOfPeriods, meteringPointId));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    private static string CreateSignature(int numberOfPeriods = 1, string? meteringPointId = null)
    {
        var response = new Signature
        {
            Value = "SomeSignatureValue",
            ExpiresTicks = 0,
            ExpiresOffsetTicks = 0,
            KeyVersion = "KeyVersion1",
            RequestId = default,
            AccessPeriods = Enumerable.Range(0, numberOfPeriods)
                .Select(_ =>
                    new AccessPeriod(
                        MeteringPointId: meteringPointId ?? "123456789012345678",
                        FromDate: DateTimeOffset.UtcNow.AddDays(-1),
                        ToDate: DateTimeOffset.UtcNow.AddDays(1))),
        };

        return JsonSerializer.Serialize(response);
    }
}
