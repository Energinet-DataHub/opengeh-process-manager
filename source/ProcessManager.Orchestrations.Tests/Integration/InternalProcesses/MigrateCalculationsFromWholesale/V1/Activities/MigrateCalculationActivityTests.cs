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

using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;

public class MigrateCalculationActivityTests : IClassFixture<MigrateCalculationActivityFixture>
{
    private readonly MigrateCalculationActivityFixture _fixture;

    public MigrateCalculationActivityTests(MigrateCalculationActivityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_CalculationInDatabase_When_CallingActivity_Then_CalculationIsMigrated()
    {
        // Arrange
        var existingCalculationId = Guid.Parse("5dd7bbb3-07f7-4cbd-a74f-17aaed795fa9");

        using var wholesaleContext = _fixture.WholesaleDatabaseManager.CreateDbContext();
        using var processManagerContext = _fixture.PMDatabaseManager.CreateDbContext();

        var sut = new MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1(
            wholesaleContext,
            processManagerContext,
            new OrchestrationInstanceFactory());

        // Act
        var actual = await sut.Run(new MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.ActivityInput(existingCalculationId));

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().NotBeNull().And.Contain("Migrated");
    }
}
