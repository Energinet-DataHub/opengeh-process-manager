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

using Azure.Core;
using Azure.Identity;
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using HealthChecks.Azure.Messaging.EventHubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Extensions.DependencyInjection;

public class MeasurementsClientExtensionsTests
{
    private const string EventHubName = "event-hub-name";
    private const string FullyQualifiedNamespace = "namespace.eventhub.windows.net";

    private ServiceCollection Services { get; } = new();

    [Fact]
    public void Given_TokenCredentialIsNotRegisteredAndOptionsAreConfigured_When_AddMeasurementsClient_Then_ExceptionIsThrownWhenCreatingEventHubClient()
    {
        // Arrange
        Services
            .AddInMemoryConfiguration(new Dictionary<string, string?>()
            {
                [$"{ProcessManagerComponentsOptions.SectionName}:{nameof(ProcessManagerComponentsOptions.AllowMockDependenciesForTests)}"] = "false",
                [$"{MeasurementsClientOptions.SectionName}:{nameof(MeasurementsClientOptions.FullyQualifiedNamespace)}"] = FullyQualifiedNamespace,
                [$"{MeasurementsClientOptions.SectionName}:{nameof(MeasurementsClientOptions.EventHubName)}"] = EventHubName,
            });

        // Act
        Services.AddMeasurementsClient();

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var eventHubClientFactory = serviceProvider.GetRequiredService<MeasurementsEventHubProducerClientFactory>();
        var act = () => eventHubClientFactory.Create("test-metering-point-id");
        act.Should()
            .Throw<InvalidOperationException>()
                .WithMessage("No service for type 'Energinet.DataHub.Core.App.Common.Identity.TokenCredentialProvider' has been registered*");
    }

    [Fact]
    public void Given_TokenCredentialIsRegisteredAndOptionsAreConfigured_When_AddMeasurementsClient_Then_MeasurementsClientAndEventHubClientCanBeCreated()
    {
        // Arrange
        Services
            .AddTokenCredentialProvider()
            .AddInMemoryConfiguration(new Dictionary<string, string?>()
            {
                [$"{MeasurementsClientOptions.SectionName}:{nameof(MeasurementsClientOptions.FullyQualifiedNamespace)}"] = FullyQualifiedNamespace,
                [$"{MeasurementsClientOptions.SectionName}:{nameof(MeasurementsClientOptions.EventHubName)}"] = EventHubName,
            });

        // Act
        Services.AddMeasurementsClient();

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var actualClient = serviceProvider.GetRequiredService<IMeasurementsClient>();
        actualClient.Should().BeOfType<MeasurementsClient>();

        var eventHubClientFactory = serviceProvider.GetRequiredService<MeasurementsEventHubProducerClientFactory>();
        var act = () => eventHubClientFactory.Create("test-metering-point-id");
        act.Should().NotThrow();
    }

    [Fact]
    public void Given_TokenCredentialIsRegisteredAndOptionsAreNotConfigured_When_AddMeasurementsClient_Then_ExceptionIsThrownWhenCreatingEventHubClient()
    {
        // Arrange
        Services
            .AddTokenCredentialProvider()
            .AddInMemoryConfiguration([]);

        // Act
        Services.AddMeasurementsClient();

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var eventHubClientFactory = serviceProvider.GetRequiredService<MeasurementsEventHubProducerClientFactory>();

        var act = () => eventHubClientFactory.Create("test-metering-point-id");
        act.Should()
            .Throw<OptionsValidationException>()
                .WithMessage("DataAnnotation validation failed for 'MeasurementsClientOptions'*")
            .And.Failures.Should()
                .ContainMatch("*FullyQualifiedNamespace field is required*")
                .And.ContainMatch("*The EventHubName field is required*");
    }

    [Fact]
    public void Given_AzureEventHubHealthCheckAreConfigured_When_AddMeasurementsClient_Then_MeasurementsEventHubHealthCheckIsRegistered()
    {
        // Arrange
        Services
            .AddTokenCredentialProvider()
            .AddInMemoryConfiguration(new Dictionary<string, string?>()
            {
                [$"{MeasurementsClientOptions.SectionName}:{nameof(MeasurementsClientOptions.FullyQualifiedNamespace)}"] = FullyQualifiedNamespace,
                [$"{MeasurementsClientOptions.SectionName}:{nameof(MeasurementsClientOptions.EventHubName)}"] = EventHubName,
            });
        Services.AddMeasurementsClient();
        var serviceProvider = Services.BuildServiceProvider();

        // Act
        var healthCheckRegistrations = serviceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations;

        // Assert
        var healthCheckRegistration = healthCheckRegistrations
            .Should()
            .ContainSingle()
            .Subject;
        healthCheckRegistration.Name.Should()
            .BeSameAs(MeasurementsClientOptions.SectionName);
        healthCheckRegistration.Factory(serviceProvider)
            .Should()
            .BeOfType<AzureEventHubHealthCheck>();
    }
}
