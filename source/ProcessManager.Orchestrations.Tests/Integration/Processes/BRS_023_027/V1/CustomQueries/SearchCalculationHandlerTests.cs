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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.CustomQueries;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_023_027.V1.CustomQueries;

public class SearchCalculationHandlerTests : IClassFixture<ProcessManagerDatabaseFixture>, IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly ProcessManagerContext _dbContext;
    private readonly SearchCalculationHandler _sut;

    private readonly UserIdentityDto _userIdentity = new UserIdentityDto(
        UserId: Guid.NewGuid(),
        ActorNumber: ActorNumber.Create("1111111111111"),
        ActorRole: ActorRole.DataHubAdministrator);

    public SearchCalculationHandlerTests(ProcessManagerDatabaseFixture fixture)
    {
        _fixture = fixture;
        _dbContext = fixture.DatabaseManager.CreateDbContext();
        _sut = new SearchCalculationHandler(
            new OrchestrationInstanceRepository(_dbContext));
    }

    public async Task InitializeAsync()
    {
        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        await dbContext.OrchestrationInstances.ExecuteDeleteAsync();
        await dbContext.OrchestrationDescriptions.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Given_ThreeCalculations_When_QueryIsBeforeCalculationPeriods_Then_ResultIsEmpty()
    {
        // Given
        var calculation1 = (
            // 20/2/2025
            Start: new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero));

        var calculation2 = (
            // 21/2/2025
            Start: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 21, 23, 00, 00, TimeSpan.Zero));

        var calculation3 = (
            // 23/2/2025 - 25/2/2025 (not inclusive)
            Start: new DateTimeOffset(2025, 02, 22, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero));

        await CreateCalculationOrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var calculationQuery = new CalculationQuery(_userIdentity)
        {
            // Query for 19/2/2025 - 20/2/2025 (not inclusive), should give no calculations
            PeriodStartDate = new DateTimeOffset(2025, 02, 18, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
        };

        var calculationQueryResults = await _sut.HandleAsync(calculationQuery);

        // Then
        calculationQueryResults
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Given_ThreeCalculations_When_QueryIsAfterCalculationPeriods_Then_ResultIsEmpty()
    {
        // Given
        var calculation1 = (
            // 20/2/2025
            Start: new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero));

        var calculation2 = (
            // 21/2/2025
            Start: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 21, 23, 00, 00, TimeSpan.Zero));

        var calculation3 = (
            // 23/2/2025 - 25/2/2025 (not inclusive)
            Start: new DateTimeOffset(2025, 02, 22, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero));

        await CreateCalculationOrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var calculationQuery = new CalculationQuery(_userIdentity)
        {
            // Query for 25/2/2025 - 26/2/2025 (not inclusive), should give no calculations
            PeriodStartDate = new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 25, 23, 00, 00, TimeSpan.Zero),
        };

        var calculationQueryResults = await _sut.HandleAsync(calculationQuery);

        // Then
        calculationQueryResults
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Given_ThreeCalculations_When_QueryMatchesCalculation1Period_Then_ResultIsCalculation1()
    {
        // Given
        var calculation1 = (
            // 20/2/2025
            Start: new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero));

        var calculation2 = (
            // 21/2/2025
            Start: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 21, 23, 00, 00, TimeSpan.Zero));

        var calculation3 = (
            // 23/2/2025 - 25/2/2025 (not inclusive)
            Start: new DateTimeOffset(2025, 02, 22, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero));

        await CreateCalculationOrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var calculationQuery = new CalculationQuery(_userIdentity)
        {
            // Query for 20/2/2025 - 21/2/2025 (not inclusive), should give calculation 1
            PeriodStartDate = new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
        };

        var calculationQueryResults = await _sut.HandleAsync(calculationQuery);

        // Then
        calculationQueryResults
            .Should()
            .HaveCount(1)
            .And.SatisfyRespectively(
                result =>
                {
                    result.OrchestrationInstance.ParameterValue.PeriodStartDate.Should().Be(calculation1.Start);
                    result.OrchestrationInstance.ParameterValue.PeriodEndDate.Should().Be(calculation1.End);
                });
    }

    [Fact]
    public async Task Given_ThreeCalculations_When_QueryMatchesCalculation2And3Period_Then_ResultIsCalculation2And3()
    {
        // Given
        var calculation1 = (
            // 20/2/2025
            Start: new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero));

        var calculation2 = (
            // 21/2/2025
            Start: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 21, 23, 00, 00, TimeSpan.Zero));

        var calculation3 = (
            // 23/2/2025 - 25/2/2025 (not inclusive)
            Start: new DateTimeOffset(2025, 02, 22, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero));

        await CreateCalculationOrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var calculationQuery = new CalculationQuery(_userIdentity)
        {
            // Query for 21/2/2025 - 24/2/2025 (not inclusive), should give calculation 2 and 3
            PeriodStartDate = new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 23, 23, 00, 00, TimeSpan.Zero),
        };

        var calculationQueryResults = await _sut.HandleAsync(calculationQuery);

        // Then
        calculationQueryResults
            .OrderBy(c => c.OrchestrationInstance.ParameterValue.PeriodStartDate)
            .Should()
            .HaveCount(2)
            .And.SatisfyRespectively(
                result =>
                {
                    result.OrchestrationInstance.ParameterValue.PeriodStartDate.Should().Be(calculation2.Start);
                    result.OrchestrationInstance.ParameterValue.PeriodEndDate.Should().Be(calculation2.End);
                },
                result =>
                {
                    result.OrchestrationInstance.ParameterValue.PeriodStartDate.Should().Be(calculation3.Start);
                    result.OrchestrationInstance.ParameterValue.PeriodEndDate.Should().Be(calculation3.End);
                });
    }

    [Fact]
    public async Task Given_ThreeCalculations_When_QueryMatchesAllCalculationPeriods_Then_ResultIsAllCalculations()
    {
        // Given
        var calculation1 = (
            // 20/2/2025
            Start: new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero));

        var calculation2 = (
            // 21/2/2025
            Start: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 21, 23, 00, 00, TimeSpan.Zero));

        var calculation3 = (
            // 23/2/2025 - 25/2/2025 (not inclusive)
            Start: new DateTimeOffset(2025, 02, 22, 23, 00, 00, TimeSpan.Zero),
            End: new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero));

        await CreateCalculationOrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var calculationQuery = new CalculationQuery(_userIdentity)
        {
            // Query for 19/2/2025 - 25/2/2025 (not inclusive), should give all calculations
            PeriodStartDate = new DateTimeOffset(2025, 02, 18, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero),
        };

        var calculationQueryResults = await _sut.HandleAsync(calculationQuery);

        // Then
        calculationQueryResults
            .OrderBy(c => c.OrchestrationInstance.ParameterValue.PeriodStartDate)
            .Should()
            .HaveCount(3)
            .And.SatisfyRespectively(
                result =>
                {
                    result.OrchestrationInstance.ParameterValue.PeriodStartDate.Should().Be(calculation1.Start);
                    result.OrchestrationInstance.ParameterValue.PeriodEndDate.Should().Be(calculation1.End);
                },
                result =>
                {
                    result.OrchestrationInstance.ParameterValue.PeriodStartDate.Should().Be(calculation2.Start);
                    result.OrchestrationInstance.ParameterValue.PeriodEndDate.Should().Be(calculation2.End);
                },
                result =>
                {
                    result.OrchestrationInstance.ParameterValue.PeriodStartDate.Should().Be(calculation3.Start);
                    result.OrchestrationInstance.ParameterValue.PeriodEndDate.Should().Be(calculation3.End);
                });
    }

    private async Task<IReadOnlyCollection<OrchestrationInstance>> CreateCalculationOrchestrationInstancesAsync(
        (DateTimeOffset Start, DateTimeOffset End)[] periods)
    {
        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        var descriptionBuilder = new Orchestrations.Processes.BRS_023_027.V1.Orchestration.OrchestrationDescriptionBuilder();
        var orchestrationDescription = descriptionBuilder.Build();
        dbContext.OrchestrationDescriptions.Add(orchestrationDescription);

        var orchestrationInstances = periods.Select(
            period =>
            {
                var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
                    identity: _userIdentity.MapToDomain(),
                    description: orchestrationDescription,
                    skipStepsBySequence: [],
                    clock: SystemClock.Instance);

                orchestrationInstance.ParameterValue.SetFromInstance(new CalculationInputV1(
                    CalculationType: CalculationType.Aggregation,
                    GridAreaCodes: ["111"],
                    PeriodStartDate: period.Start,
                    PeriodEndDate: period.End,
                    IsInternalCalculation: false));

                return orchestrationInstance;
            })
            .ToList();

        dbContext.OrchestrationInstances.AddRange(orchestrationInstances);
        await dbContext.SaveChangesAsync();

        return orchestrationInstances;
    }
}
