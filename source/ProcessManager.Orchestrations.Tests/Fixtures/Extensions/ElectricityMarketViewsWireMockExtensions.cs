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
using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Microsoft.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

public static class ElectricityMarketViewsWireMockExtensions
{
    /// <summary>
    /// Mock <see cref="IElectricityMarketViews"/> <see cref="IElectricityMarketViews.GetMeteringPointMasterDataChangesAsync"/> endpoint, which can be found at:
    /// https://github.com/Energinet-DataHub/geh-electricity-market/blob/main/source/electricity-market/ElectricityMarket.Integration/ElectricityMarketViews.cs
    /// </summary>
    public static WireMockServer MockElectricityMarketViewsMasterData(
        this WireMockServer server,
        IEnumerable<MeteringPointMasterData> mockData)
    {
        var request = Request
            .Create()
            .WithPath("/api/get-metering-point-master-data")
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(JsonSerializer.Serialize(mockData));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }
}
