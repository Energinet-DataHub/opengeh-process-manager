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
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Moq;
using NodaTime;
using ApiModel = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.CustomQueries.Calculations.V1;

public class SearchCalculationsHandlerV1Tests :
    IClassFixture<ProcessManagerDatabaseFixture>,
    IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly ProcessManagerReaderContext _readerContext;
    private readonly SearchCalculationsHandlerV1 _sut;

    private readonly UserIdentityDto _userIdentity = new UserIdentityDto(
        UserId: Guid.NewGuid(),
        ActorNumber: ActorNumber.Create("1111111111111"),
        ActorRole: ActorRole.DataHubAdministrator);

    private readonly IOrchestrationDescriptionBuilder _wholesaleCalculationDescriptionBuilder = new
        Orchestrations.Processes.BRS_023_027.V1
        .Orchestration.OrchestrationDescriptionBuilder();

    private readonly IOrchestrationDescriptionBuilder _electricalHeatingDescriptionBuilder = new
        Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1
        .Orchestration.OrchestrationDescriptionBuilder();

    private readonly IOrchestrationDescriptionBuilder _netConsumptionDescriptionBuilder = new
        Orchestrations.Processes.BRS_021.NetConsumptionCalculation.V1
        .Orchestration.OrchestrationDescriptionBuilder();

    private readonly IOrchestrationDescriptionBuilder _capacitySettlementDescriptionBuilder = new
        Orchestrations.Processes.BRS_021.CapacitySettlementCalculation.V1
        .Orchestration.OrchestrationDescriptionBuilder();

    public SearchCalculationsHandlerV1Tests(ProcessManagerDatabaseFixture fixture)
    {
        _fixture = fixture;
        _readerContext = fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();

        _sut = new SearchCalculationsHandlerV1(_readerContext);
    }

    public async Task InitializeAsync()
    {
        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        await dbContext.OrchestrationInstances.ExecuteDeleteAsync();
        await dbContext.OrchestrationDescriptions.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await _readerContext.DisposeAsync();
    }

    [Fact]
    public async Task Given_LifecycleDatasetInDatabase_When_SearchByRunning_Then_OnlyExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(_electricalHeatingDescriptionBuilder);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(_netConsumptionDescriptionBuilder);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            LifecycleStates = [ApiModel.OrchestrationInstanceLifecycleState.Running],
        };

        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should()
            .HaveCount(2)
            .And.Satisfy(
                result => result is ElectricalHeatingCalculationResultV1 && ((ElectricalHeatingCalculationResultV1)result).Id == electricalHeating.IsRunning.Id.Value,
                result => result is NetConsumptionCalculationResultV1 && ((NetConsumptionCalculationResultV1)result).Id == netConsumption.IsRunning.Id.Value);
    }

    [Fact]
    public async Task Given_LifecycleDatasetInDatabase_When_SearchByQueuedAndRunning_Then_OnlyExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(_electricalHeatingDescriptionBuilder);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(_netConsumptionDescriptionBuilder);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            LifecycleStates = [
                ApiModel.OrchestrationInstanceLifecycleState.Queued,
                ApiModel.OrchestrationInstanceLifecycleState.Running],
        };

        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should()
            .HaveCount(4)
            .And.Satisfy(
                result => result is ElectricalHeatingCalculationResultV1 && ((ElectricalHeatingCalculationResultV1)result).Id == electricalHeating.IsQueued.Id.Value,
                result => result is ElectricalHeatingCalculationResultV1 && ((ElectricalHeatingCalculationResultV1)result).Id == electricalHeating.IsRunning.Id.Value,
                result => result is NetConsumptionCalculationResultV1 && ((NetConsumptionCalculationResultV1)result).Id == netConsumption.IsQueued.Id.Value,
                result => result is NetConsumptionCalculationResultV1 && ((NetConsumptionCalculationResultV1)result).Id == netConsumption.IsRunning.Id.Value);
    }

    /// <summary>
    /// Here we also search for 'BalanceFixing' even though we know there isn't any in the database.
    /// This impacts the JSON search as it will also search for Wholesale calculation types.
    /// </summary>
    [Fact]
    public async Task Given_LifecycleDatasetInDatabase_When_SearchByCalculationTypeAndPending_Then_OnlyExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(_electricalHeatingDescriptionBuilder);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(_netConsumptionDescriptionBuilder);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = [
                CalculationTypeQueryParameterV1.NetConsumption,
                CalculationTypeQueryParameterV1.BalanceFixing],
            LifecycleStates = [ApiModel.OrchestrationInstanceLifecycleState.Pending],
        };

        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is NetConsumptionCalculationResultV1 && ((NetConsumptionCalculationResultV1)result).Id == netConsumption.IsPending.Id.Value);
    }

    [Fact]
    public async Task Given_LifecycleDatasetInDatabase_When_SearchByTerminationState_Then_OnlyExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(_electricalHeatingDescriptionBuilder);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(_netConsumptionDescriptionBuilder);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            LifecycleStates = [ApiModel.OrchestrationInstanceLifecycleState.Terminated],
            TerminationState = ApiModel.OrchestrationInstanceTerminationState.Succeeded,
        };

        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should()
            .HaveCount(2)
            .And.Satisfy(
                result => result is ElectricalHeatingCalculationResultV1 && ((ElectricalHeatingCalculationResultV1)result).Id == electricalHeating.IsTerminatedAsSucceeded.Id.Value,
                result => result is NetConsumptionCalculationResultV1 && ((NetConsumptionCalculationResultV1)result).Id == netConsumption.IsTerminatedAsSucceeded.Id.Value);
    }

    [Fact]
    public async Task Given_LifecycleDatasetInDatabase_When_SearchByCalculationTypeAndTerminationState_Then_OnlyExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(_electricalHeatingDescriptionBuilder);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(_netConsumptionDescriptionBuilder);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = [CalculationTypeQueryParameterV1.ElectricalHeating],
            LifecycleStates = [ApiModel.OrchestrationInstanceLifecycleState.Terminated],
            TerminationState = ApiModel.OrchestrationInstanceTerminationState.Failed,
        };

        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is ElectricalHeatingCalculationResultV1 && ((ElectricalHeatingCalculationResultV1)result).Id == electricalHeating.IsTerminatedAsFailed.Id.Value);
    }

    [Fact]
    public async Task Given_LifecycleDatasetInDatabase_When_SearchByStartedAtOrLater_Then_OnlyExpectedCalculationsAreRetrieved()
    {
        // Given
        var electricalHeatingIsRunningStartedAt = SystemClock.Instance.GetCurrentInstant().PlusDays(1);

        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(
            _electricalHeatingDescriptionBuilder,
            isRunningStartedAt: electricalHeatingIsRunningStartedAt);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(
            _netConsumptionDescriptionBuilder);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            StartedAtOrLater = electricalHeatingIsRunningStartedAt.ToDateTimeOffset(),
        };

        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is ElectricalHeatingCalculationResultV1 && ((ElectricalHeatingCalculationResultV1)result).Id == electricalHeating.IsRunning.Id.Value);
    }

    [Fact]
    public async Task Given_LifecycleDatasetInDatabase_When_SearchByTerminatedAtOrEarlier_Then_OnlyExpectedCalculationsAreRetrieved()
    {
        // Given
        var netConsumptionIsTerminatedAsSucceededAt = SystemClock.Instance.GetCurrentInstant().PlusDays(-1);

        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(
            _electricalHeatingDescriptionBuilder);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(
            _netConsumptionDescriptionBuilder,
            isTerminatedAsSucceededAt: netConsumptionIsTerminatedAsSucceededAt);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            TerminatedAtOrEarlier = netConsumptionIsTerminatedAsSucceededAt.ToDateTimeOffset(),
        };

        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is NetConsumptionCalculationResultV1 && ((NetConsumptionCalculationResultV1)result).Id == netConsumption.IsTerminatedAsSucceeded.Id.Value);
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

        await Create_Brs_023_027_OrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 19/2/2025 - 20/2/2025 (not inclusive), should give no calculations
            PeriodStartDate = new DateTimeOffset(2025, 02, 18, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
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

        await Create_Brs_023_027_OrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 25/2/2025 - 26/2/2025 (not inclusive), should give no calculations
            PeriodStartDate = new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 25, 23, 00, 00, TimeSpan.Zero),
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
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

        await Create_Brs_023_027_OrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 20/2/2025 - 21/2/2025 (not inclusive), should give calculation 1
            PeriodStartDate = new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Select(c => c.As<WholesaleCalculationResultV1>())
            .Should()
            .HaveCount(1)
            .And.SatisfyRespectively(
                result =>
                {
                    result.ParameterValue.PeriodStartDate.Should().Be(calculation1.Start);
                    result.ParameterValue.PeriodEndDate.Should().Be(calculation1.End);
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

        await Create_Brs_023_027_OrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 21/2/2025 - 24/2/2025 (not inclusive), should give calculation 2 and 3
            PeriodStartDate = new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 23, 23, 00, 00, TimeSpan.Zero),
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Select(c => c.As<WholesaleCalculationResultV1>())
            .OrderBy(c => c.ParameterValue.PeriodStartDate)
            .Should()
            .HaveCount(2)
            .And.SatisfyRespectively(
                result =>
                {
                    result.ParameterValue.PeriodStartDate.Should().Be(calculation2.Start);
                    result.ParameterValue.PeriodEndDate.Should().Be(calculation2.End);
                },
                result =>
                {
                    result.ParameterValue.PeriodStartDate.Should().Be(calculation3.Start);
                    result.ParameterValue.PeriodEndDate.Should().Be(calculation3.End);
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

        await Create_Brs_023_027_OrchestrationInstancesAsync([
            calculation1,
            calculation2,
            calculation3]);

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 19/2/2025 - 25/2/2025 (not inclusive), should give all calculations
            PeriodStartDate = new DateTimeOffset(2025, 02, 18, 23, 00, 00, TimeSpan.Zero),
            PeriodEndDate = new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero),
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Select(c => c.As<WholesaleCalculationResultV1>())
            .OrderBy(c => c.ParameterValue.PeriodStartDate)
            .Should()
            .HaveCount(3)
            .And.SatisfyRespectively(
                result =>
                {
                    result.ParameterValue.PeriodStartDate.Should().Be(calculation1.Start);
                    result.ParameterValue.PeriodEndDate.Should().Be(calculation1.End);
                },
                result =>
                {
                    result.ParameterValue.PeriodStartDate.Should().Be(calculation2.Start);
                    result.ParameterValue.PeriodEndDate.Should().Be(calculation2.End);
                },
                result =>
                {
                    result.ParameterValue.PeriodStartDate.Should().Be(calculation3.Start);
                    result.ParameterValue.PeriodEndDate.Should().Be(calculation3.End);
                });
    }

    /// <summary>
    /// We also seed database with the John Doe dataset, to ensure the JSON search doesn't
    /// cause exceptions if there isn't JSON in the columns.
    /// </summary>
    [Fact]
    public async Task Given_CalculationsForGridAreas_When_SearchByAnotherGridArea_Then_ResultIsEmpty()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        await SeedDatabaseWithWholesaleCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            GridAreaCodes = ["333"],
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .BeEmpty();
    }

    /// <summary>
    /// We also seed database with the John Doe dataset, to ensure the JSON search doesn't
    /// cause exceptions if there isn't JSON in the columns.
    /// </summary>
    [Fact]
    public async Task Given_CalculationsForGridAreas_When_SearchByMatchingGridArea_Then_ExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var orchestrationInstances = await SeedDatabaseWithWholesaleCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            GridAreaCodes = ["222"],
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(2)
            .And.Satisfy(
                result => result is WholesaleCalculationResultV1 && ((WholesaleCalculationResultV1)result).Id == orchestrationInstances.BalanceFixing.Id.Value,
                result => result is WholesaleCalculationResultV1 && ((WholesaleCalculationResultV1)result).Id == orchestrationInstances.WholesaleFixing.Id.Value);
    }

    /// <summary>
    /// We also seed database with the John Doe dataset, to ensure the JSON search doesn't
    /// cause exceptions if there isn't JSON in the columns.
    /// </summary>
    [Fact]
    public async Task Given_InternalCalculation_When_SearchByIsInternalCalculation_Then_AggregationCalculationIsRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var orchestrationInstances = await SeedDatabaseWithWholesaleCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            IsInternalCalculation = true,
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is WholesaleCalculationResultV1 && ((WholesaleCalculationResultV1)result).Id == orchestrationInstances.Aggregation.Id.Value);
    }

    /// <summary>
    /// We also seed database with the John Doe dataset, to ensure the JSON search doesn't
    /// cause exceptions if there isn't JSON in the columns.
    /// </summary>
    [Fact]
    public async Task Given_WholesaleCalculationsDataset_When_SearchByCalculationTypes_Then_ExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var orchestrationInstances = await SeedDatabaseWithWholesaleCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = [
                CalculationTypeQueryParameterV1.Aggregation,
                CalculationTypeQueryParameterV1.WholesaleFixing],
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(2)
            .And.Satisfy(
                result => result is WholesaleCalculationResultV1 && ((WholesaleCalculationResultV1)result).Id == orchestrationInstances.Aggregation.Id.Value,
                result => result is WholesaleCalculationResultV1 && ((WholesaleCalculationResultV1)result).Id == orchestrationInstances.WholesaleFixing.Id.Value);
    }

    /// <summary>
    /// We also seed database with the John Doe dataset, to ensure the JSON search doesn't
    /// cause exceptions if there isn't JSON in the columns.
    /// </summary>
    [Fact]
    public async Task Given_CapacitySettlementDataset_When_SearchByPeriodWhichDoesNotContainCalculations_Then_ResultIsEmpty()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var orchestrationInstances = await SeedDatabaseWithCapacitySettlementCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 1/9/2024 - 1/10/2024 (not inclusive)
            PeriodStartDate = new DateTimeOffset(2024, 8, 31, 22, 00, 00, TimeSpan.Zero), // Summertime
            PeriodEndDate = new DateTimeOffset(2024, 9, 30, 22, 00, 00, TimeSpan.Zero), // Summertime
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Given_CapacitySettlementDataset_When_SearchByPeriodWhichFullyContainsCalculations_Then_AllCalculationsAreRetrieved()
    {
        // Given
        var orchestrationInstances = await SeedDatabaseWithCapacitySettlementCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 1/9/2024 - 1/3/2025 (not inclusive)
            PeriodStartDate = new DateTimeOffset(2024, 8, 31, 22, 00, 00, TimeSpan.Zero), // Summertime
            PeriodEndDate = new DateTimeOffset(2025, 2, 28, 23, 00, 00, TimeSpan.Zero), // Wintertime
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(2)
            .And.Satisfy(
                result => result is CapacitySettlementCalculationResultV1 && ((CapacitySettlementCalculationResultV1)result).Id == orchestrationInstances.October2024.Id.Value,
                result => result is CapacitySettlementCalculationResultV1 && ((CapacitySettlementCalculationResultV1)result).Id == orchestrationInstances.February2025.Id.Value);
    }

    [Fact]
    public async Task Given_CapacitySettlementDataset_When_SearchByPeriodWhichFullyContainsOctober2024_Then_October2024IsRetrieved()
    {
        // Given
        var orchestrationInstances = await SeedDatabaseWithCapacitySettlementCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 1/9/2024 - 1/11/2024 (not inclusive)
            PeriodStartDate = new DateTimeOffset(2024, 8, 31, 22, 00, 00, TimeSpan.Zero), // Summertime
            PeriodEndDate = new DateTimeOffset(2024, 10, 31, 23, 00, 00, TimeSpan.Zero), // Wintertime
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is CapacitySettlementCalculationResultV1 && ((CapacitySettlementCalculationResultV1)result).Id == orchestrationInstances.October2024.Id.Value);
    }

    [Fact]
    public async Task Given_CapacitySettlementDataset_When_SearchByPeriodWhichPartlyContainsAllCalculations_Then_AllCalculationsAreRetrieved()
    {
        // Given
        var orchestrationInstances = await SeedDatabaseWithCapacitySettlementCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 11/10/2024 - 3/2/2024 (not inclusive)
            PeriodStartDate = new DateTimeOffset(2024, 10, 10, 22, 00, 00, TimeSpan.Zero), // Summertime
            PeriodEndDate = new DateTimeOffset(2025, 2, 2, 23, 00, 00, TimeSpan.Zero), // Wintertime
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(2)
            .And.Satisfy(
                result => result is CapacitySettlementCalculationResultV1 && ((CapacitySettlementCalculationResultV1)result).Id == orchestrationInstances.October2024.Id.Value,
                result => result is CapacitySettlementCalculationResultV1 && ((CapacitySettlementCalculationResultV1)result).Id == orchestrationInstances.February2025.Id.Value);
    }

    /// <summary>
    /// We also seed database with the John Doe dataset, to ensure the JSON search doesn't
    /// cause exceptions if there isn't JSON in the columns.
    /// </summary>
    [Fact]
    public async Task Given_CapacitySettlementAndWholesaleDataset_When_SearchByPeriodWhichContainsCalculationFromBothDataset_Then_ExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var capacitySettlementInstances = await SeedDatabaseWithCapacitySettlementCalculationsDatasetAsync();
        var wholesaleInstances = await SeedDatabaseWithWholesaleCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // Query for 23/2/2025 - 1/3/2025 (not inclusive)
            PeriodStartDate = new DateTimeOffset(2025, 2, 22, 23, 00, 00, TimeSpan.Zero), // Wintertime
            PeriodEndDate = new DateTimeOffset(2025, 2, 28, 23, 00, 00, TimeSpan.Zero), // Wintertime
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(2)
            .And.Satisfy(
                result => result is CapacitySettlementCalculationResultV1 && ((CapacitySettlementCalculationResultV1)result).Id == capacitySettlementInstances.February2025.Id.Value,
                result => result is WholesaleCalculationResultV1 && ((WholesaleCalculationResultV1)result).Id == wholesaleInstances.WholesaleFixing.Id.Value);
    }

    /// <summary>
    /// The intention of this test is to use as much as possible of the query in SQL.
    /// </summary>
    [Fact]
    public async Task Given_MixOfCalculationsDataset_When_SearchByAllCommonQueryParameters_Then_ExpectedCalculationsAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(_electricalHeatingDescriptionBuilder);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(_netConsumptionDescriptionBuilder);
        var capacitySettlementInstances = await SeedDatabaseWithCapacitySettlementCalculationsDatasetAsync();
        var wholesaleInstances = await SeedDatabaseWithWholesaleCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // => Common fields
            CalculationTypes = [
                CalculationTypeQueryParameterV1.BalanceFixing,
                CalculationTypeQueryParameterV1.WholesaleFixing,
                CalculationTypeQueryParameterV1.ElectricalHeating,
                CalculationTypeQueryParameterV1.CapacitySettlement,
                CalculationTypeQueryParameterV1.NetConsumption],
            LifecycleStates = [
                ApiModel.OrchestrationInstanceLifecycleState.Pending,
                ApiModel.OrchestrationInstanceLifecycleState.Running],
            TerminationState = null,
            ScheduledAtOrLater = null,
            StartedAtOrLater = new DateTimeOffset(2020, 2, 22, 23, 00, 00, TimeSpan.Zero), // Wintertime
            TerminatedAtOrEarlier = null,
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(2)
            .And.Satisfy(
                result => result is ElectricalHeatingCalculationResultV1 && ((ElectricalHeatingCalculationResultV1)result).Id == electricalHeating.IsRunning.Id.Value,
                result => result is NetConsumptionCalculationResultV1 && ((NetConsumptionCalculationResultV1)result).Id == netConsumption.IsRunning.Id.Value);
    }

    /// <summary>
    /// The intention of this test is to use as much as possible of the query in SQL.
    /// Because we use "Wholesale" query parameters, only those types will be retrieved.
    /// </summary>
    [Fact]
    public async Task Given_MixOfCalculationsDataset_When_SearchByAllPossibleWholesaleQueryParameters_Then_ExpectedWholesaleCalculationIsRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var electricalHeating = await SeedDatabaseWithLifecycleDatasetAsync(_electricalHeatingDescriptionBuilder);
        var netConsumption = await SeedDatabaseWithLifecycleDatasetAsync(_netConsumptionDescriptionBuilder);
        var capacitySettlementInstances = await SeedDatabaseWithCapacitySettlementCalculationsDatasetAsync();
        var wholesaleInstances = await SeedDatabaseWithWholesaleCalculationsDatasetAsync();

        // When
        var query = new CalculationsQueryV1(_userIdentity)
        {
            // => Common fields
            CalculationTypes = [
                CalculationTypeQueryParameterV1.BalanceFixing,
                CalculationTypeQueryParameterV1.WholesaleFixing,
                CalculationTypeQueryParameterV1.ElectricalHeating,
                CalculationTypeQueryParameterV1.CapacitySettlement,
                CalculationTypeQueryParameterV1.NetConsumption],
            LifecycleStates = [
                ApiModel.OrchestrationInstanceLifecycleState.Pending,
                ApiModel.OrchestrationInstanceLifecycleState.Running],
            TerminationState = null,
            ScheduledAtOrLater = null,
            StartedAtOrLater = null,
            TerminatedAtOrEarlier = null,

            // => Wholesale calculations
            IsInternalCalculation = false,
            GridAreaCodes = ["222"],

            // => Wholesale + Capacity settlement calculations
            // Query for 23/2/2025 - 1/3/2025 (not inclusive)
            PeriodStartDate = new DateTimeOffset(2025, 2, 22, 23, 00, 00, TimeSpan.Zero), // Wintertime
            PeriodEndDate = new DateTimeOffset(2025, 2, 28, 23, 00, 00, TimeSpan.Zero), // Wintertime
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is WholesaleCalculationResultV1 && ((WholesaleCalculationResultV1)result).Id == wholesaleInstances.WholesaleFixing.Id.Value);
    }

    private async Task<(
            OrchestrationInstance October2024,
            OrchestrationInstance February2025)>
        SeedDatabaseWithCapacitySettlementCalculationsDatasetAsync()
    {
        var orchestrationDescription = _capacitySettlementDescriptionBuilder.Build();

        var october2024 = CreateCapacitySettlementCalculation(
            orchestrationDescription,
            new Abstractions.Processes.BRS_021.CapacitySettlementCalculation.V1.Model.CalculationInputV1(
                Year: 2024,
                Month: 10));

        var february2025 = CreateCapacitySettlementCalculation(
            orchestrationDescription,
            new Abstractions.Processes.BRS_021.CapacitySettlementCalculation.V1.Model.CalculationInputV1(
                Year: 2025,
                Month: 2));

        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        dbContext.OrchestrationDescriptions.Add(orchestrationDescription);
        dbContext.OrchestrationInstances.Add(october2024);
        dbContext.OrchestrationInstances.Add(february2025);
        await dbContext.SaveChangesAsync();

        return (october2024, february2025);
    }

    private OrchestrationInstance CreateCapacitySettlementCalculation(
        OrchestrationDescription orchestrationDescription,
        Abstractions.Processes.BRS_021.CapacitySettlementCalculation.V1.Model.CalculationInputV1 input)
    {
        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);

        orchestrationInstance.ParameterValue.SetFromInstance(input);

        return orchestrationInstance;
    }

    private async Task Create_Brs_023_027_OrchestrationInstancesAsync(
        (DateTimeOffset Start, DateTimeOffset End)[] periods)
    {
        var orchestrationDescription = _wholesaleCalculationDescriptionBuilder.Build();

        var orchestrationInstances = periods.Select(
            period =>
            {
                var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
                    identity: _userIdentity.MapToDomain(),
                    description: orchestrationDescription,
                    skipStepsBySequence: [],
                    clock: SystemClock.Instance);

                orchestrationInstance.ParameterValue.SetFromInstance(
                    new Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1(
                        CalculationType: Abstractions.Processes.BRS_023_027.V1.Model.CalculationType.Aggregation,
                        GridAreaCodes: ["111"],
                        PeriodStartDate: period.Start,
                        PeriodEndDate: period.End,
                        IsInternalCalculation: false));

                return orchestrationInstance;
            })
            .ToList();

        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        dbContext.OrchestrationDescriptions.Add(orchestrationDescription);
        dbContext.OrchestrationInstances.AddRange(orchestrationInstances);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Create 3 types of wholesale calculations, with various input:
    ///  - Calculation types used are:
    ///     - Aggregation
    ///     - BalanceFixing
    ///     - WholesaleFixing
    ///  - GridAreaCodes used are:
    ///     - "111"
    ///     - "222"
    ///  - Aggregation is an internal calcaultion; others are external
    /// </summary>
    private async Task<(
            OrchestrationInstance Aggregation,
            OrchestrationInstance BalanceFixing,
            OrchestrationInstance WholesaleFixing)>
        SeedDatabaseWithWholesaleCalculationsDatasetAsync()
    {
        var orchestrationDescription = _wholesaleCalculationDescriptionBuilder.Build();

        var aggregation = CreateWholesaleCalculation(
            orchestrationDescription,
            new Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1(
                CalculationType: Abstractions.Processes.BRS_023_027.V1.Model.CalculationType.Aggregation,
                GridAreaCodes: ["111"],
                // 20/2/2025
                PeriodStartDate: new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
                PeriodEndDate: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
                // Internal
                IsInternalCalculation: true));

        var balanceFixing = CreateWholesaleCalculation(
            orchestrationDescription,
            new Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1(
                CalculationType: Abstractions.Processes.BRS_023_027.V1.Model.CalculationType.BalanceFixing,
                GridAreaCodes: ["222"],
                // 21/2/2025
                PeriodStartDate: new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
                PeriodEndDate: new DateTimeOffset(2025, 02, 21, 23, 00, 00, TimeSpan.Zero),
                // External
                IsInternalCalculation: false));

        var wholesaleFixing = CreateWholesaleCalculation(
            orchestrationDescription,
            new Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1(
                CalculationType: Abstractions.Processes.BRS_023_027.V1.Model.CalculationType.WholesaleFixing,
                GridAreaCodes: ["111", "222"],
                // 23/2/2025 - 25/2/2025 (not inclusive)
                PeriodStartDate: new DateTimeOffset(2025, 02, 22, 23, 00, 00, TimeSpan.Zero),
                PeriodEndDate: new DateTimeOffset(2025, 02, 24, 23, 00, 00, TimeSpan.Zero),
                // External
                IsInternalCalculation: false));

        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        dbContext.OrchestrationDescriptions.Add(orchestrationDescription);
        dbContext.OrchestrationInstances.Add(aggregation);
        dbContext.OrchestrationInstances.Add(balanceFixing);
        dbContext.OrchestrationInstances.Add(wholesaleFixing);
        await dbContext.SaveChangesAsync();

        return (aggregation, balanceFixing, wholesaleFixing);
    }

    private OrchestrationInstance CreateWholesaleCalculation(
        OrchestrationDescription orchestrationDescription,
        Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1 calculationInput)
    {
        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);

        orchestrationInstance.ParameterValue.SetFromInstance(calculationInput);

        return orchestrationInstance;
    }

    /// <summary>
    /// Create an orchestration description using the given builder.
    /// Create orchestration instances in the following lifecycle states:
    ///  - Pending
    ///  - Queued
    ///  - Running
    ///  - Terminated as succeeded
    ///  - Terminated as failed
    ///
    /// If <paramref name="isRunningStartedAt"/> is specified, then this value
    /// is used when transitioning to Running.
    ///
    /// If <paramref name="isTerminatedAsSucceededAt"/> is specified, then this value
    /// is used when transitioning to terminated as succeeded.
    /// </summary>
    private async Task<(
            OrchestrationInstance IsPending,
            OrchestrationInstance IsQueued,
            OrchestrationInstance IsRunning,
            OrchestrationInstance IsTerminatedAsSucceeded,
            OrchestrationInstance IsTerminatedAsFailed)>
        SeedDatabaseWithLifecycleDatasetAsync(
            IOrchestrationDescriptionBuilder builder,
            Instant isRunningStartedAt = default,
            Instant isTerminatedAsSucceededAt = default)
    {
        var orchestrationDescription = builder.Build();
        var orchestrationInstances = CreateLifecycleDataset(
            orchestrationDescription,
            isRunningStartedAt,
            isTerminatedAsSucceededAt);

        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        dbContext.OrchestrationDescriptions.Add(orchestrationDescription);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsPending);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsQueued);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsRunning);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsTerminatedAsSucceeded);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsTerminatedAsFailed);
        await dbContext.SaveChangesAsync();

        return orchestrationInstances;
    }

    /// <summary>
    /// Create orchestration instances from <paramref name="orchestrationDescription"/>
    /// in the following lifecycle states:
    ///  - Pending
    ///  - Queued
    ///  - Running
    ///  - Terminated as succeeded
    ///  - Terminated as failed
    ///
    /// If <paramref name="isRunningStartedAt"/> is specified, then this value
    /// is used when transitioning to Running.
    ///
    /// If <paramref name="isTerminatedAsSucceededAt"/> is specified, then this value
    /// is used when transitioning to terminated as succeeded.
    /// </summary>
    private (
            OrchestrationInstance IsPending,
            OrchestrationInstance IsQueued,
            OrchestrationInstance IsRunning,
            OrchestrationInstance IsTerminatedAsSucceeded,
            OrchestrationInstance IsTerminatedAsFailed)
        CreateLifecycleDataset(
            OrchestrationDescription orchestrationDescription,
            Instant isRunningStartedAt = default,
            Instant isTerminatedAsSucceededAt = default)
    {
        var isPending = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);

        var isQueued = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
        isQueued.Lifecycle.TransitionToQueued(SystemClock.Instance);

        var isRunning = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
        isRunning.Lifecycle.TransitionToQueued(SystemClock.Instance);
        if (isRunningStartedAt == default)
        {
            isRunning.Lifecycle.TransitionToRunning(SystemClock.Instance);
        }
        else
        {
            var clockMock = new Mock<IClock>();
            clockMock.Setup(m => m.GetCurrentInstant())
                .Returns(isRunningStartedAt);
            isRunning.Lifecycle.TransitionToRunning(clockMock.Object);
        }

        var isTerminatedAsSucceeded = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
        isTerminatedAsSucceeded.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isTerminatedAsSucceeded.Lifecycle.TransitionToRunning(SystemClock.Instance);
        if (isTerminatedAsSucceededAt == default)
        {
            isTerminatedAsSucceeded.Lifecycle.TransitionToSucceeded(SystemClock.Instance);
        }
        else
        {
            var clockMock = new Mock<IClock>();
            clockMock.Setup(m => m.GetCurrentInstant())
                .Returns(isTerminatedAsSucceededAt);
            isTerminatedAsSucceeded.Lifecycle.TransitionToSucceeded(clockMock.Object);
        }

        var isTerminatedAsFailed = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
        isTerminatedAsFailed.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isTerminatedAsFailed.Lifecycle.TransitionToRunning(SystemClock.Instance);
        isTerminatedAsFailed.Lifecycle.TransitionToFailed(SystemClock.Instance);

        return (isPending, isQueued, isRunning, isTerminatedAsSucceeded, isTerminatedAsFailed);
    }

    /// <summary>
    /// Create an orchestration description that isn't one of the calculation types orchestration descriptions.
    /// Create orchestration instances in all possible lifecycle states:
    ///  - Pending
    ///  - Queued
    ///  - Running
    ///  - Terminated as succeeded
    ///  - Terminated as failed
    ///  - Terminated as user cancelled (require the instance is scheduled)
    /// </summary>
    private async Task SeedDatabaseWithJohnDoeLifecycleDatasetAsync()
    {
        var johnDoeName = Guid.NewGuid().ToString();
        var johnDoeV1Description = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(name: johnDoeName, version: 1),
            canBeScheduled: true,
            functionName: "TestOrchestrationFunction");

        var johnDoe = CreateLifecycleDataset(johnDoeV1Description);

        var isTerminatedAsUserCancelled = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: johnDoeV1Description,
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            runAt: SystemClock.Instance.GetCurrentInstant());
        isTerminatedAsUserCancelled.Lifecycle.TransitionToUserCanceled(SystemClock.Instance, _userIdentity.MapToDomain());

        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        dbContext.OrchestrationDescriptions.Add(johnDoeV1Description);
        dbContext.OrchestrationInstances.Add(johnDoe.IsPending);
        dbContext.OrchestrationInstances.Add(johnDoe.IsQueued);
        dbContext.OrchestrationInstances.Add(johnDoe.IsRunning);
        dbContext.OrchestrationInstances.Add(johnDoe.IsTerminatedAsSucceeded);
        dbContext.OrchestrationInstances.Add(johnDoe.IsTerminatedAsFailed);
        dbContext.OrchestrationInstances.Add(isTerminatedAsUserCancelled);
        await dbContext.SaveChangesAsync();
    }
}
