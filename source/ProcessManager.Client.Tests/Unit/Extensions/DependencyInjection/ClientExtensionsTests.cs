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

using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Unit.Extensions.DependencyInjection;

public class ClientExtensionsTests
{
    private const string ApplicationIdUriForTests = "https://management.azure.com";

    private const string GeneralApiBaseAddressFake = "https://www.fake-general.com";
    private const string OrchestrationsApiBaseAddressFake = "https://www.fake-orchestrations.com";

    private const string ServiceBusNamespace = "namespace.servicebus.windows.net";
    private const string ServiceBusTopicName = "topic-name";

    public ClientExtensionsTests()
    {
        Services = new ServiceCollection();
    }

    private ServiceCollection Services { get; }

    [Fact]
    public void OptionsAreConfigured_WhenAddProcessManagerHttpClients_ClientCanBeCreated()
    {
        // Arrange
        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"] = ApplicationIdUriForTests,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"] = GeneralApiBaseAddressFake,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"] = OrchestrationsApiBaseAddressFake,
        });

        // Act
        Services.AddProcessManagerHttpClients();

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var actualClient = serviceProvider.GetRequiredService<IProcessManagerClient>();
        actualClient.Should().BeOfType<ProcessManagerClient>();
    }

    [Fact]
    public void OptionsAreNotConfigured_WhenAddProcessManagerHttpClients_ExceptionIsThrownWhenRequestingClient()
    {
        // Arrange
        Services.AddInMemoryConfiguration([]);

        // Act
        Services.AddProcessManagerHttpClients();

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var clientAct = () => serviceProvider.GetRequiredService<IProcessManagerClient>();
        clientAct.Should()
            .Throw<OptionsValidationException>()
                .WithMessage("DataAnnotation validation failed for 'ProcessManagerHttpClientsOptions'*")
            .And.Failures.Should()
                .ContainMatch("*GeneralApiBaseAddress field is required*")
                .And.ContainMatch("*OrchestrationsApiBaseAddress field is required*")
                .And.ContainMatch("*ApplicationIdUri field is required*");
    }

    [Fact]
    public void OptionsAreConfiguredAndAddProcessManagerHttpClients_WhenCreatingEachHttpClient_HttpClientCanBeCreatedWithExpectedBaseAddress()
    {
        // Arrange
        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}"] = ApplicationIdUriForTests,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"] = GeneralApiBaseAddressFake,
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"] = OrchestrationsApiBaseAddressFake,
        });

        Services.AddProcessManagerHttpClients();

        // Act & Assert
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
    public void ServiceBusClientIsRegisteredAndOptionsAreConfigured_WhenAddProcessManagerMessageClient_ClientCanBeCreated()
    {
        // Arrange
        Services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(ServiceBusNamespace));

        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"] = ServiceBusTopicName,
        });

        // Act
        Services.AddProcessManagerMessageClient();

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var actualClient = serviceProvider.GetRequiredService<IProcessManagerMessageClient>();
        actualClient.Should().BeOfType<ProcessManagerMessageClient>();
    }

    [Fact]
    public void ServiceBusClientIsRegisteredAndOptionsAreNotConfigured_WhenAddProcessManagerMessageClient_ExceptionIsThrownWhenRequestingClient()
    {
        // Arrange
        Services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(ServiceBusNamespace));

        Services.AddInMemoryConfiguration([]);

        // Act
        Services.AddProcessManagerMessageClient();

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var clientAct = () => serviceProvider.GetRequiredService<IProcessManagerMessageClient>();
        clientAct.Should()
            .Throw<OptionsValidationException>()
                .WithMessage("DataAnnotation validation failed for 'ProcessManagerServiceBusClientOptions'*")
            .And.Failures.Should()
                .ContainMatch("*TopicName field is required*");
    }

    [Fact]
    public void ServiceBusClientIsNotRegisteredAndOptionsAreConfigured_WhenAddProcessManagerMessageClient_ExceptionIsThrownWhenRequestingClient()
    {
        // Arrange
        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"] = ServiceBusTopicName,
        });

        // Act
        Services.AddProcessManagerMessageClient();

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var clientAct = () => serviceProvider.GetRequiredService<IProcessManagerMessageClient>();
        clientAct.Should()
            .Throw<InvalidOperationException>()
                .WithMessage("No service for type 'Azure.Messaging.ServiceBus.ServiceBusClient' has been registered*");
    }
}
