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
using Microsoft.Azure.Databricks.Client.Models;
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
        Sut = ServiceProvider.GetRequiredKeyedService<IDatabricksJobsClient>(_wholesaleSectionName);
    }

    public IDatabricksJobsClient Sut { get;  }

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
        // Arrange
        var jobId = new JobRunId(123);
        var jobName = "jobName";

        MockServer
            .MockJobsList(jobId.Id, jobName)
            .MockJobsRunNow(jobId.Id);

        // Act
        var actual = await Sut.StartJobAsync(jobName, new List<string>());

        // Assert
        actual.Should().NotBeNull();
    }

    [Theory]
    [InlineData(RunStatusState.PENDING, RunTerminationCode.SUCCESS, JobRunStatus.Pending)]
    [InlineData(RunStatusState.QUEUED, RunTerminationCode.SUCCESS, JobRunStatus.Queued)]
    [InlineData(RunStatusState.RUNNING, RunTerminationCode.SUCCESS, JobRunStatus.Running)]

    // Completed
    [InlineData(RunStatusState.TERMINATED, RunTerminationCode.SUCCESS, JobRunStatus.Completed)]
    [InlineData(RunStatusState.TERMINATING, RunTerminationCode.SUCCESS, JobRunStatus.Completed)]

    // Canceled
    [InlineData(RunStatusState.TERMINATING, RunTerminationCode.USER_CANCELED, JobRunStatus.Canceled)]
    [InlineData(RunStatusState.TERMINATING, RunTerminationCode.CANCELED, JobRunStatus.Canceled)]

    // Failed
    [InlineData(RunStatusState.TERMINATING, RunTerminationCode.RUN_EXECUTION_ERROR, JobRunStatus.Failed)]
    [InlineData(RunStatusState.BLOCKED, RunTerminationCode.RUN_EXECUTION_ERROR, JobRunStatus.Failed)]

    public async Task GetRunStatusAsync_WhenCalledWithKnownJobRunId_ReturnsExpectedRunStatus(RunStatusState status, RunTerminationCode code, JobRunStatus expectedStatus)
    {
        // Arrange
        var jobId = new JobRunId(123);

        MockServer
            .MockJobsRunsGet(jobId.Id, "jobName", status, code);

        // Act
        var actual = await Sut.GetJobRunStatusAsync(jobId);

        // Assert
        actual.Should().Be(expectedStatus);
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
