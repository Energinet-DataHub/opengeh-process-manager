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
        var existingCalculationId = Guid.Parse("a1d54d74-a5c0-45c9-af9f-49acd8cfba19");

        // Act
        await using var readDbContext = _fixture.DatabaseManager.CreateDbContext();
        var calculation = await readDbContext.Calculations.FindAsync(existingCalculationId);

        // Assert
        using var assertionScope = new AssertionScope();
        calculation.Should().NotBeNull();
        calculation!.GridAreaCodes.Should().BeEquivalentTo(new[]
        {
            new GridAreaCode("042"),
            new GridAreaCode("853"),
        });
        calculation!.PeriodStart.Should().Be(Instant.FromUtc(2024, 12, 31, 23, 0));
        calculation.PeriodEnd.Should().Be(Instant.FromUtc(2025, 1, 01, 23, 0));
        calculation.CalculationType.Should().Be(CalculationType.Aggregation);
        calculation.CreatedByUserId.Should().Be(Guid.Parse("14fc5005-e99e-4a45-5d58-08dc313ce97b"));
        calculation.CreatedTime.Should().Be(Instant.FromDateTimeOffset(DateTimeOffset.Parse("2025-01-02T08:40:38.1754566Z")));
        calculation.OrchestrationState.Should().Be(CalculationOrchestrationState.CalculationFailed);
        calculation.OrchestrationInstanceId.Id.Should().Be("cb852a951f554f5cad921ddf87b63fc3");
        calculation.CanceledByUserId.Should().Be(null);
        calculation.IsInternalCalculation.Should().Be(true);
    }
}
