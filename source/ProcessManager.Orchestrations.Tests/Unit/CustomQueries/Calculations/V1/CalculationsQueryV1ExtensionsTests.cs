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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.CapacitySettlementCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.NetConsumptionCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.CustomQueries.Calculations.V1;

public class CalculationsQueryV1ExtensionsTests
{
    private readonly UserIdentityDto _userIdentity = new(
        UserId: Guid.NewGuid(),
        ActorNumber: ActorNumber.Create("1111111111111"),
        ActorRole: ActorRole.DataHubAdministrator);

    [Fact]
    public void Given_AnyOneWholesaleCalculationType_When_GetOrchestrationDescriptionNames_Then_ReturnsBrs_023_027()
    {
        // Given
        var calculationQuery = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = [GetRandomWholesaleCalculationType()],
        };

        // When
        var actual = calculationQuery.GetOrchestrationDescriptionNames();

        // Then
        actual.Should()
            .OnlyContain(item => item == Brs_023_027.Name);
    }

    [Fact]
    public void Given_AllWholesaleCalculationTypes_When_GetOrchestrationDescriptionNames_Then_ReturnsBrs_023_027()
    {
        // Given
        var calculationQuery = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = [
                CalculationTypeQueryParameterV1.BalanceFixing,
                CalculationTypeQueryParameterV1.Aggregation,
                CalculationTypeQueryParameterV1.WholesaleFixing,
                CalculationTypeQueryParameterV1.FirstCorrectionSettlement,
                CalculationTypeQueryParameterV1.SecondCorrectionSettlement,
                CalculationTypeQueryParameterV1.ThirdCorrectionSettlement],
        };

        // When
        var actual = calculationQuery.GetOrchestrationDescriptionNames();

        // Then
        actual.Should()
            .OnlyContain(item => item == Brs_023_027.Name);
    }

    [Fact]
    public void Given_AllCalculationTypes_When_GetOrchestrationDescriptionNames_Then_ReturnsAllOrchestrationDescriptionNames()
    {
        // Given
        var calculationQuery = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = GetAllCalculationTypes(),
        };

        // When
        var actual = calculationQuery.GetOrchestrationDescriptionNames();

        // Then
        actual.Should()
            .Contain([
                Brs_021_ElectricalHeatingCalculation.Name,
                Brs_021_CapacitySettlementCalculation.Name,
                Brs_021_NetConsumptionCalculation.Name,
                Brs_023_027.Name]);
    }

    [Fact]
    public void Given_AllButWholesaleCalculationTypes_When_GetOrchestrationDescriptionNames_Then_ReturnsAllOrchestrationDescriptionNamesExceptBrs_023_027()
    {
        // Given
        var calculationQuery = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = [
                CalculationTypeQueryParameterV1.ElectricalHeating,
                CalculationTypeQueryParameterV1.CapacitySettlement,
                CalculationTypeQueryParameterV1.NetConsumption,
                CalculationTypeQueryParameterV1.MissingMeasurementsLog,
            ],
        };

        // When
        var actual = calculationQuery.GetOrchestrationDescriptionNames();

        // Then
        actual.Should()
            .Contain([
                Brs_021_ElectricalHeatingCalculation.Name,
                Brs_021_CapacitySettlementCalculation.Name,
                Brs_021_NetConsumptionCalculation.Name,
                Brs_045_MissingMeasurementsLogCalculation.Name,
            ])
            .And.NotContain([
                Brs_023_027.Name]);
    }

    [Fact]
    public void Given_AllCalculationTypesAndIsInternalCalculation_When_GetOrchestrationDescriptionNames_Then_ReturnsOnlyBrs_023_027()
    {
        // Given
        var calculationQuery = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = GetAllCalculationTypes(),
            IsInternalCalculation = true,
        };

        // When
        var actual = calculationQuery.GetOrchestrationDescriptionNames();

        // Then
        actual.Should()
            .OnlyContain(item => item == Brs_023_027.Name);
    }

    [Fact]
    public void Given_AllCalculationTypesAndGridAreaCodesIsSpecified_When_GetOrchestrationDescriptionNames_Then_ReturnsOnlyBrs_023_027()
    {
        // Given
        var calculationQuery = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = GetAllCalculationTypes(),
            GridAreaCodes = ["804"],
        };

        // When
        var actual = calculationQuery.GetOrchestrationDescriptionNames();

        // Then
        actual.Should()
            .OnlyContain(item => item == Brs_023_027.Name);
    }

    [Fact]
    public void Given_AllCalculationTypesAndPeriodStartIsSpecified_When_GetOrchestrationDescriptionNames_Then_ReturnsOnlyBrs_023_027AndBrs_021_CapacitySettlement()
    {
        // Given
        var calculationQuery = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = GetAllCalculationTypes(),
            PeriodStartDate = new DateTimeOffset(2025, 02, 19, 23, 00, 00, TimeSpan.Zero),
        };

        // When
        var actual = calculationQuery.GetOrchestrationDescriptionNames();

        // Then
        actual.Should()
            .Contain([
                Brs_021_CapacitySettlementCalculation.Name,
                Brs_023_027.Name])
            .And.NotContain([
                Brs_021_ElectricalHeatingCalculation.Name,
                Brs_021_NetConsumptionCalculation.Name,
                Brs_045_MissingMeasurementsLogCalculation.Name,
            ]);
    }

    [Fact]
    public void Given_AllCalculationTypesAndPeriodEndIsSpecified_When_GetOrchestrationDescriptionNames_Then_ReturnsOnlyBrs_023_027AndBrs_021_CapacitySettlement()
    {
        // Given
        var calculationQuery = new CalculationsQueryV1(_userIdentity)
        {
            CalculationTypes = GetAllCalculationTypes(),
            PeriodEndDate = new DateTimeOffset(2025, 02, 20, 23, 00, 00, TimeSpan.Zero),
        };

        // When
        var actual = calculationQuery.GetOrchestrationDescriptionNames();

        // Then
        actual.Should()
            .Contain([
                Brs_021_CapacitySettlementCalculation.Name,
                Brs_023_027.Name])
            .And.NotContain([
                Brs_021_ElectricalHeatingCalculation.Name,
                Brs_021_NetConsumptionCalculation.Name,
                Brs_045_MissingMeasurementsLogCalculation.Name,
            ]);
    }

    private static CalculationTypeQueryParameterV1 GetRandomWholesaleCalculationType()
    {
        return (CalculationTypeQueryParameterV1)new Random().Next(0, 6);
    }

    private static IReadOnlyCollection<CalculationTypeQueryParameterV1> GetAllCalculationTypes()
        => Enum.GetValues<CalculationTypeQueryParameterV1>();
}
