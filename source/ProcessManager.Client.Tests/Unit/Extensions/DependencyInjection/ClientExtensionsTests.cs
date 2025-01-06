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

using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Unit.Extensions.DependencyInjection;

public class ClientExtensionsTests
{
    private const string GeneralApiBaseAddressFake = "https://www.fake-general.com";
    private const string OrchestrationsApiBaseAddressFake = "https://www.fake-orchestrations.com";

    public ClientExtensionsTests()
    {
        Services = new ServiceCollection();
    }

    private ServiceCollection Services { get; }

    [Fact]
    public void AddProcessManagerHttpClientsAndConfigured_WhenCreatingClients_ClientsCanBeCreated()
    {
        // Arrange
        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"] = GeneralApiBaseAddressFake,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"] = OrchestrationsApiBaseAddressFake,
        });

        // Act
        Services.AddProcessManagerHttpClients();

        // Assert
        using var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        // => Factory
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        // => General API client
        var generalApiClient = httpClientFactory.CreateClient(HttpClientNames.GeneralApi);
        generalApiClient.BaseAddress.Should().Be(GeneralApiBaseAddressFake);

        // => Orchestrations API client
        var orchestrationsApiClient = httpClientFactory.CreateClient(HttpClientNames.OrchestrationsApi);
        orchestrationsApiClient.BaseAddress.Should().Be(OrchestrationsApiBaseAddressFake);
    }

    [Fact]
    public void AddProcessManagerHttpClientsAndNotConfigured_WhenCreatingClients_ExceptionIsThrown()
    {
        // Arrange
        Services.AddInMemoryConfiguration([]);

        // Act
        Services.AddProcessManagerHttpClients();

        // Assert
        using var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        // => Factory
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        // => General API client
        var generalApiClientAct = () => httpClientFactory.CreateClient(HttpClientNames.GeneralApi);
        generalApiClientAct.Should()
            .Throw<OptionsValidationException>()
            .WithMessage("*'The GeneralApiBaseAddress field is required.'*");

        // => Orchestrations API client
        var orchestrationsApiClientAct = () => httpClientFactory.CreateClient(HttpClientNames.OrchestrationsApi);
        orchestrationsApiClientAct.Should()
            .Throw<OptionsValidationException>()
            .WithMessage("*'The OrchestrationsApiBaseAddress field is required.'*");
    }
}
