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

using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Databricks.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProcessManager.Components.Extensions.DependencyInjection;
using ProcessManager.Components.Extensions.Options;
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
        using var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        var actualJobsApi = serviceProvider.GetRequiredService<IJobsApi>();
        actualJobsApi.Should().NotBeNull();
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
}
