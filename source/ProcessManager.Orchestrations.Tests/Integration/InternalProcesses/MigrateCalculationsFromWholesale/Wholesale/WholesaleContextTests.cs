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

using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale.Model;
using FluentAssertions;
using FluentAssertions.Execution;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;

public class WholesaleContextTests : IClassFixture<WholesaleDatabaseFixture>
{
    private readonly WholesaleDatabaseFixture _fixture;

    public WholesaleContextTests(WholesaleDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_CalculationInDatabase_When_RetrievingFromDatabase_Then_HasExpectedValues()
    {
        // Arrange
        var existingCalculationId = Guid.Parse("d49e67f3-b17e-4a6b-bfb3-73ffa9077985");

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var calculation = await readDbContext.Calculations.FindAsync(existingCalculationId);

        // Assert
        using var assertionScope = new AssertionScope();
        calculation.Should().NotBeNull();
        calculation!.GridAreaCodes.Should().BeEquivalentTo(new[]
        {
            new GridAreaCode("533"),
            new GridAreaCode("543"),
            new GridAreaCode("584"),
            new GridAreaCode("803"),
            new GridAreaCode("804"),
            new GridAreaCode("950"),
        });
        calculation!.PeriodStart.Should().Be(Instant.FromUtc(2022, 12, 31, 23, 0));
        calculation.PeriodEnd.Should().Be(Instant.FromUtc(2023, 1, 31, 23, 0));
        calculation.CalculationType.Should().Be(CalculationType.Aggregation);
        calculation.CreatedByUserId.Should().Be(Guid.Parse("5bfaa00b-cfbe-440f-15ef-08dc65f606a6"));
        calculation.CreatedTime.Should().Be(Instant.FromDateTimeOffset(DateTimeOffset.Parse("2024-09-05T09:57:58.6951104Z")));
        calculation.OrchestrationState.Should().Be(CalculationOrchestrationState.Completed);
        calculation.OrchestrationInstanceId.Id.Should().Be("b20e306a66c6423594b59bb91b1f321a");
        calculation.CanceledByUserId.Should().Be(null);
        calculation.IsInternalCalculation.Should().Be(true);
    }
}
