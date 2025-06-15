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
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Server;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Authorization;

public class AuthorizationClientTests : IAsyncLifetime
{
    private readonly ActorNumber _actorNumber = ActorNumber.Create("1234567890123");
    private readonly ActorRole _actorRole = ActorRole.EnergySupplier;
    private readonly MeteringPointId _meteringPointId = new MeteringPointId("123456789012345678");
    private readonly RequestedPeriod _requestedPeriod = new RequestedPeriod(
        DateTimeOffset.UtcNow.AddDays(-30),
        DateTimeOffset.UtcNow);

    public AuthorizationClientTests()
    {
        MockServer = WireMockServer.Start(port: 8888);
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

    private WireMockServer MockServer { get; }

    private ServiceCollection Services { get; }

    private ServiceProvider ServiceProvider { get; }

    [Fact]
    public async Task Given_GoodResponse_When_GetAuthorizedPeriodsAsync_Then_ReturnPeriods()
    {
        // Arrange
        MockServer.MockGetAuthorizedPeriodsAsync(
            numberOfPeriods: 1,
            meteringPointId: _meteringPointId.Value);

        // Act
        var actual = await Sut.GetAuthorizedPeriodsAsync(
            _actorNumber,
            _actorRole,
            _meteringPointId,
            _requestedPeriod);

        actual.Should().ContainSingle().Subject.MeteringPointId.Should().Be(_meteringPointId);
    }

    /// <summary>
    /// I'm unable to make:
    /// .ReadFromJsonAsync<Signature/>() (ignore / at the end)
    /// .ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to deserialize signature response content");
    /// fail in the AuthorizationRequestService. Since I cant test the use case described
    /// </summary>
    [Fact]
    public async Task Given_GoodResponseWithNoPeriod_When_GetAuthorizedPeriodsAsync_Then_ReturnNoPeriods()
    {
        // Arrange
        MockServer.MockGetAuthorizedPeriodsAsync(
            numberOfPeriods: 0,
            meteringPointId: _meteringPointId.Value);

        // Act
        var actual = await Sut.GetAuthorizedPeriodsAsync(
            _actorNumber,
            _actorRole,
            _meteringPointId,
            _requestedPeriod);

        actual.Should().BeEmpty();
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
