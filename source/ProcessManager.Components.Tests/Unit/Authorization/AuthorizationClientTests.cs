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

using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Authorization;
using Energinet.DataHub.ProcessManager.Components.Authorization.Model;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Server;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Authorization;

public class AuthorizationClientTests : IAsyncLifetime
{
    public AuthorizationClientTests()
    {
        MockServer = WireMockServer.Start(port: 8989);
        Services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AuthorizationClientOptions.SectionName}:{nameof(AuthorizationClientOptions.BaseUrl)}"]
                    = MockServer.Url,
            })
            .Build();

        Services
            .AddScoped<IConfiguration>(_ => configuration)
            .AddTokenCredentialProvider();

        AuthorizationClientExtensions.AddAuthorizationClient(Services, configuration);

        //Services.AddAuthorizationClient(configuration);
        ServiceProvider = Services.BuildServiceProvider();
        Sut = ServiceProvider.GetRequiredService<IAuthorizationClient>();
    }

    private IAuthorizationClient Sut { get;  }

    private WireMockServer MockServer { get; set; }

    private ServiceCollection Services { get; }

    private ServiceProvider ServiceProvider { get; }

    [Fact]
    public async Task Given_GoodResponseFromMarkPart_When_GetAuthorizedPeriodsAsync_Then_ReturnPeriods()
    {
        // Arrange
        var actorNumber = ActorNumber.Create("1234567890123");
        var actorRole = ActorRole.EnergySupplier;
        var meteringPointId = new MeteringPointId("123456789012345678");
        var requestedPeriod = new RequestedPeriod(
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow);

        // Act
        var actual = await Sut.GetAuthorizedPeriodsAsync(actorNumber, actorRole, meteringPointId, requestedPeriod);

        actual.Should().AllSatisfy(period => period.MeteringPointId.Should().Be(meteringPointId));
    }

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
}
