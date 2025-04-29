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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.EnqueueActorMessages;

public class EnqueueActorMessagesSyncClientTests : IAsyncLifetime
{
    public EnqueueActorMessagesSyncClientTests()
    {
        MockServer = WireMockServer.Start(port: 8989);
        Services = new ServiceCollection();

        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            [$"{EdiEnqueueActorMessageSyncClientOptions.SectionName}:{nameof(EdiEnqueueActorMessageSyncClientOptions.Url)}"] = MockServer.Url,
        });

        Services.AddEnqueueActorMessagesSync();
        ServiceProvider = Services.BuildServiceProvider();
        Sut = ServiceProvider.GetRequiredService<IEnqueueActorMessagesSyncClient>();
    }

    private IEnqueueActorMessagesSyncClient Sut { get;  }

    private WireMockServer MockServer { get; set; }

    private ServiceCollection Services { get; }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        MockServer.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        MockServer.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Given_SuccessfulResponse_When_EnqueueAsync_Then_NoExceptions()
    {
        var actor = new Actor(
            ActorNumber: ActorNumber.Create("1234567890123"),
            ActorRole: ActorRole.EnergySupplier);

        var request = new EnqueueData(actor);

        MockEnqueueRequest(MockServer, request, HttpStatusCode.OK);

        await Sut.EnqueueAsync(request);
    }

    [Fact]
    public async Task Given_FaultedResponse_When_EnqueueAsync_Then_ThrowsException()
    {
        var actor = new Actor(
            ActorNumber: ActorNumber.Create("1234567890123"),
            ActorRole: ActorRole.EnergySupplier);

        var request = new EnqueueData(actor);

        MockEnqueueRequest(MockServer, request, HttpStatusCode.RequestTimeout);

        await Assert.ThrowsAsync<HttpRequestException>(() => Sut.EnqueueAsync(request))
        ;
    }

    private static void MockEnqueueRequest(WireMockServer server, IEnqueueDataSyncDto enqueueData, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var request = Request
            .Create()
            .WithPath($"{EnqueueActorMessagesSyncClient.EdiEndpointPrefix}{enqueueData.Route}")
            .UsingPost();

        var response = Response
            .Create()
            .WithStatusCode(statusCode)
            .WithHeader(HeaderNames.ContentType, "application/json");

        server
            .Given(request)
            .RespondWith(response);
    }

    private record EnqueueData(
        Actor Receiver)
        : IEnqueueDataSyncDto
    {
        public const string RouteName = "v1/enqueue_actor_messages";

        public string Route => RouteName;
    }
}
