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

using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs;
using Energinet.DataHub.ProcessManager.Components.Diagnostics.HealthChecks;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Extensions.DependencyInjection;

public class DatabricksWorkspaceExtensionsTests
{
    private const string BaseUrlFake = "https://www.fake.com";
    private const string TokenFake = "not-empty";

    public DatabricksWorkspaceExtensionsTests()
    {
        Services = new ServiceCollection();
    }

    private ServiceCollection Services { get; }

    [Fact]
    public void AddDatabricksJobs_WhenDefaultSectionName_RegistrationsArePerformed()
    {
        // Arrange
        AddInMemoryConfigurations(new Dictionary<string, string?>()
        {
            [$"{DatabricksWorkspaceOptions.SectionName}:{nameof(DatabricksWorkspaceOptions.BaseUrl)}"] = BaseUrlFake,
            [$"{DatabricksWorkspaceOptions.SectionName}:{nameof(DatabricksWorkspaceOptions.Token)}"] = TokenFake,
        });

        // Act
        Services.AddDatabricksJobs();

        // Assert
        var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        // => Service
        var actualJobsClient = serviceProvider.GetRequiredService<IDatabricksJobsClient>();
        actualJobsClient.Should().NotBeNull();

        // => Health check
        var healthCheckRegistrations = serviceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations;

        healthCheckRegistrations
            .Should()
            .ContainSingle();

        var healthCheckRegistration = healthCheckRegistrations.Single();

        healthCheckRegistration.Name.Should().Contain("Databricks Jobs API");
        healthCheckRegistration.Factory(serviceProvider)
            .Should()
            .BeOfType<DatabricksJobsApiHealthCheck>();
    }

    [Fact]
    public void AddDatabricksJobs_WhenCustomSectionName_RegistrationsArePerformedUsingKey()
    {
        // Arrange
        var sectionName = "Custom";
        AddInMemoryConfigurations(new Dictionary<string, string?>()
        {
            [$"{sectionName}:{nameof(DatabricksWorkspaceOptions.BaseUrl)}"] = BaseUrlFake,
            [$"{sectionName}:{nameof(DatabricksWorkspaceOptions.Token)}"] = TokenFake,
        });

        // Act
        Services.AddDatabricksJobs(configSectionPath: sectionName);

        // Assert
        var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        // => Service
        var actualJobsClient = serviceProvider.GetRequiredKeyedService<IDatabricksJobsClient>(serviceKey: sectionName);
        actualJobsClient.Should().NotBeNull();

        // => Health check
        var healthCheckRegistrations = serviceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations;

        healthCheckRegistrations
            .Should()
            .ContainSingle();

        var healthCheckRegistration = healthCheckRegistrations.Single();

        healthCheckRegistration.Name.Should().Contain(sectionName).And.Contain("Databricks Jobs API");
        healthCheckRegistration.Factory(serviceProvider)
            .Should()
            .BeOfType<DatabricksJobsApiHealthCheck>();
    }

    [Fact]
    public void AddDatabricksJobs_WhenConfiguringMultipleWorkspaces_CanUseFromKeyedServicesToInjectClients()
    {
        // Arrange
        var wholesaleSectionName = "Wholesale";
        var measurementsSectionName = "Measurements";
        AddInMemoryConfigurations(new Dictionary<string, string?>()
        {
            [$"{wholesaleSectionName}:{nameof(DatabricksWorkspaceOptions.BaseUrl)}"] = "https://www.wholesale.com",
            [$"{wholesaleSectionName}:{nameof(DatabricksWorkspaceOptions.Token)}"] = TokenFake,
            [$"{measurementsSectionName}:{nameof(DatabricksWorkspaceOptions.BaseUrl)}"] = "https://www.measurements.com",
            [$"{measurementsSectionName}:{nameof(DatabricksWorkspaceOptions.Token)}"] = TokenFake,
        });

        Services.AddDatabricksJobs(configSectionPath: wholesaleSectionName);
        Services.AddDatabricksJobs(configSectionPath: measurementsSectionName);

        // Act
        Services.AddTransient<WholesaleClientStub>();
        Services.AddTransient<MeasurementsClientStub>();

        // Assert
        var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        // => Wholesale
        var actualWholesaleClient = serviceProvider.GetRequiredService<WholesaleClientStub>();
        actualWholesaleClient.Client.Should().NotBeNull();

        // => Measurements
        var actualMeasurementsClient = serviceProvider.GetRequiredService<MeasurementsClientStub>();
        actualMeasurementsClient.Client.Should().NotBeNull();
    }

    private void AddInMemoryConfigurations(Dictionary<string, string?> configurations)
    {
        Services.AddScoped<IConfiguration>(_ =>
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(configurations)
                .Build();
        });
    }

    public class WholesaleClientStub
    {
        /// <summary>
        /// The "key" must match the configuration section name give during registration.
        /// </summary>
        public WholesaleClientStub([FromKeyedServices("Wholesale")] IDatabricksJobsClient client)
        {
            Client = client;
        }

        public IDatabricksJobsClient Client { get; }
    }

    public class MeasurementsClientStub
    {
        /// <summary>
        /// The "key" must match the configuration section name give during registration.
        /// </summary>
        public MeasurementsClientStub([FromKeyedServices("Measurements")] IDatabricksJobsClient client)
        {
            Client = client;
        }

        public IDatabricksJobsClient Client { get; }
    }
}
