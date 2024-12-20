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

using Energinet.DataHub.ProcessManager.Components.Diagnostics.HealthChecks;
using FluentAssertions;
using Microsoft.Azure.Databricks.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Diagnostics.HealthChecks;

public sealed class DatabricksJobsApiHealthCheckTests
{
    private readonly Mock<DatabricksClient> _databricksClientMock;
    private readonly HealthCheckContext _healthCheckContext;

    public DatabricksJobsApiHealthCheckTests()
    {
        _databricksClientMock = new Mock<DatabricksClient>();

        // Configure HealthCheckContext.Registration.FailureStatus
        _healthCheckContext = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("serviceName", Mock.Of<IHealthCheck>(), HealthStatus.Unhealthy, default),
        };
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabricksJobRequestIsFailing_ReturnUnhealthyStatus()
    {
        // Arrange
        _databricksClientMock
            .Setup(mock => mock.Jobs.ListPageable(default, default, default, default))
            .Throws(new Exception("Any exception"));

        var sut = new DatabricksJobsApiHealthCheck(_databricksClientMock.Object);

        // Act
        var actualResponse = await sut.CheckHealthAsync(_healthCheckContext!, default);

        // Assert
        actualResponse.Status.ToString()
            .Should().Be(Enum.GetName(typeof(HealthStatus), HealthStatus.Unhealthy));
    }
}
