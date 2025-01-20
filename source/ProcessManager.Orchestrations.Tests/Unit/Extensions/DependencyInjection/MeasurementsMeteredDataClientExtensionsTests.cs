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

using Azure.Core;
using Azure.Identity;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Components.DataHub.Measurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using HealthChecks.Azure.Messaging.EventHubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Extensions.DependencyInjection;

public class MeasurementsMeteredDataClientExtensionsTests
{
    private const string EventHubName = "event-hub-name";
    private const string EventHubNamespace = "namespace.eventhub.windows.net";

    private static readonly TokenCredential _azureCredential = new DefaultAzureCredential();

    private ServiceCollection Services { get; } = new();

    [Fact]
    public void MeasurementsMeteredDataClientOptionsAreConfigured_WhenAddMeasurementsMeteredDataClient_ClientCanBeCreated()
    {
        // Arrange
        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.NamespaceName)}"] = EventHubNamespace,
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.EventHubName)}"] = EventHubName,
        });

        // Act
        Services.AddMeasurementsMeteredDataClient(_azureCredential);

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var actualClient = serviceProvider.GetRequiredService<IMeasurementsMeteredDataClient>();
        actualClient.Should().BeOfType<MeasurementsMeteredDataClient>();
    }

    [Fact]
    public void MeasurementsMeteredDataClientOptionsAreNotConfigured_WhenAddMeasurementsMeteredDataClient_ExceptionIsThrownWhenRequestingClient()
    {
        // Arrange
        Services.AddInMemoryConfiguration([]);

        // Act
        Services.AddMeasurementsMeteredDataClient(_azureCredential);

        // Assert
        var serviceProvider = Services.BuildServiceProvider();

        var clientAct = () => serviceProvider.GetRequiredService<IMeasurementsMeteredDataClient>();
        clientAct.Should()
            .Throw<OptionsValidationException>()
                .WithMessage("DataAnnotation validation failed for 'MeasurementsMeteredDataClientOptions'*")
            .And.Failures.Should()
                .ContainMatch("*NamespaceName field is required*")
                .And.ContainMatch("*The EventHubName field is required*");
    }

    [Fact]
    public void AzureEventHubHealthCheckAreConfigured_WhenAddMeasurementsMeteredDataClient_MeasurementsEventHubHealthCheckIsRegistered()
    {
        // Arrange
        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.NamespaceName)}"] = EventHubNamespace,
            [$"{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.EventHubName)}"] = EventHubName,
        });
        Services.AddMeasurementsMeteredDataClient(_azureCredential);
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
            .BeSameAs(MeasurementsMeteredDataClientOptions.SectionName);
        healthCheckRegistration.Factory(serviceProvider)
            .Should()
            .BeOfType<AzureEventHubHealthCheck>();
    }
}
