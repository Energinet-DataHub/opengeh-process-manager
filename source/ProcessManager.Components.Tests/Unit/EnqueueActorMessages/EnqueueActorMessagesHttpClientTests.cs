﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.ProcessManager.Components.Abstractions.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Server;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.EnqueueActorMessages;

public class EnqueueActorMessagesHttpClientTests : IAsyncLifetime
{
    public EnqueueActorMessagesHttpClientTests()
    {
        MockServer = WireMockServer.Start(port: 8989);
        Services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{EnqueueActorMessagesHttpClientOptions.SectionName}:{nameof(EnqueueActorMessagesHttpClientOptions.BaseUrl)}"]
                    = MockServer.Url,
                [$"{EnqueueActorMessagesHttpClientOptions.SectionName}:{nameof(EnqueueActorMessagesHttpClientOptions.ApplicationIdUri)}"]
                    = SubsystemAuthenticationOptionsForTests.ApplicationIdUri,
            })
            .Build();

        Services
            .AddScoped<IConfiguration>(_ => configuration)
            .AddTokenCredentialProvider();

        Services.AddEnqueueActorMessagesHttp(configuration);
        ServiceProvider = Services.BuildServiceProvider();
        Sut = ServiceProvider.GetRequiredService<IEnqueueActorMessagesHttpClient>();
    }

    private IEnqueueActorMessagesHttpClient Sut { get;  }

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
        var request = new TestEnqueueData();

        MockServer.MockEnqueueActorMessagesHttpResponse(
            request.Route,
            HttpStatusCode.OK);

        await Sut.EnqueueAsync(request);
    }

    [Fact]
    public async Task Given_FaultedResponse_When_EnqueueAsync_Then_ThrowsException()
    {
        var request = new TestEnqueueData();

        MockServer.MockEnqueueActorMessagesHttpResponse(
            request.Route,
            HttpStatusCode.RequestTimeout);

        await Assert.ThrowsAsync<HttpRequestException>(() => Sut.EnqueueAsync(request));
    }

    private record TestEnqueueData
        : IEnqueueDataSyncDto
    {
        public const string RouteName = "v1/enqueue_actor_messages";

        public string Route => RouteName;
    }
}
