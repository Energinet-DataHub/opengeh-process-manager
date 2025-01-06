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
using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Server;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Databricks.Jobs;

public class DatabricksJobsClientWiremockTests : IAsyncLifetime
{
    private readonly string _wholesaleSectionName = "Wholesale";

    public DatabricksJobsClientWiremockTests()
    {
        MockServer = WireMockServer.Start(port: 1111);
        Services = new ServiceCollection();

        AddInMemoryConfigurations(new Dictionary<string, string?>()
        {
            [$"{_wholesaleSectionName}:{nameof(DatabricksWorkspaceOptions.BaseUrl)}"] = MockServer.Url,
            [$"{_wholesaleSectionName}:{nameof(DatabricksWorkspaceOptions.Token)}"] = "not-empty",
        });

        Services.AddDatabricksJobs(_wholesaleSectionName);

        ServiceProvider = Services.BuildServiceProvider();
    }

    public WireMockServer MockServer { get; set; }

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
    public async Task StartJobAsync_WhenCallingWithoutParameters_ReturnsJobRunId()
    {
        var jobId = new JobRunId(123);
        var jobName = "jobName";

        MockServer
            .MockJobsList(jobId.Id, jobName)
            .MockJobsRunNow(jobId.Id);

        var sut = ServiceProvider.GetRequiredKeyedService<IDatabricksJobsClient>(_wholesaleSectionName);

        // Act
        var actual = await sut.StartJobAsync(jobName, new List<string>());

        // Assert
        actual.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRunStatusAsync_WhenCalledWithKnownJobRunId_ReturnsExpectedRunStatus()
    {
        var jobId = new JobRunId(123);
        var jobName = "jobName";

        MockServer
            .MockJobsRunsGet(jobId.Id, "TERMINATED", "SUCCESS", jobName);

        var sut = ServiceProvider.GetRequiredKeyedService<IDatabricksJobsClient>(_wholesaleSectionName);

        // Act
        var actual = await sut.GetJobRunStatusAsync(jobId);

        // Assert
        actual.Should().Be(JobRunStatus.Completed);
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
