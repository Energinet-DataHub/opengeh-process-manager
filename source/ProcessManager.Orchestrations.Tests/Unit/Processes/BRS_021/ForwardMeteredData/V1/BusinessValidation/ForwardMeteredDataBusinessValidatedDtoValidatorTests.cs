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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class ForwardMeteredDataBusinessValidatedDtoValidatorTests
{
    private readonly Mock<IClock> _clockMock = new Mock<IClock>();
    private readonly Mock<IFeatureFlagManager> _featureFlagManagerMock = new Mock<IFeatureFlagManager>();
    private readonly DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;

    private readonly BusinessValidator<ForwardMeteredDataBusinessValidatedDto> _sut;

    public ForwardMeteredDataBusinessValidatedDtoValidatorTests()
    {
        _clockMock.Setup(c => c.GetCurrentInstant())
            .Returns(Instant.FromUtc(2024, 11, 15, 16, 46, 43));

        _featureFlagManagerMock.Setup(f => f
                .IsEnabledAsync(FeatureFlag.EnableBrs021ForwardMeteredDataPerformanceTest))
            .ReturnsAsync(true);

        IServiceCollection services = new ServiceCollection();

        services.AddLogging();
        services.AddTransient<DateTimeZone>(s => _timeZone);
        services.AddTransient<IClock>(s => _clockMock.Object);
        services.AddSingleton<IFeatureFlagManager>(s => _featureFlagManagerMock.Object);

        var orchestrationsAssembly = typeof(OrchestrationDescriptionBuilder).Assembly;
        var orchestrationsAbstractionsAssembly =
            typeof(ForwardMeteredDataBusinessValidatedDto).Assembly;
        services.AddBusinessValidation(assembliesToScan: [orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        var serviceProvider = services.BuildServiceProvider();

        _sut = serviceProvider
            .GetRequiredService<BusinessValidator<ForwardMeteredDataBusinessValidatedDto>>();
    }

    [Fact]
    public async Task Given_ValidForwardMeteredDataBusinessValidatedDto_When_Validate_Then_NoValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(input);

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                    meteringPointMasterData,
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_InvalidEndDate_When_Validate_Then_InvalidEndDateValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithEndDateTime(null)
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                endDateTime: "2025-04-24T13:37Z"); // Master data must have valid end date time

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));

        result.Should()
            .ContainSingle()
            .And.ContainEquivalentOf(PeriodValidationRule.InvalidEndDate);
    }

    [Fact]
    public async Task Given_NoMasterData_When_Validate_Then_ValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: []));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointValidationRule.MeteringPointDoesntExistsError);
    }

    [Fact]
    public async Task Given_WrongMeteringPointOwner_When_Validate_Then_InvalidMeteringPointOwnershipValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithGridAccessProviderNumber("1111111111111")
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                gridAccessProvider: "9999999999999"); // Different owner in master data compared to the input

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointOwnershipValidationRule.MeteringPointHasWrongOwnerError);
    }

    [Fact]
    public async Task Given_InvalidConnectionState_When_Validate_Then_ValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        const ConnectionState invalidConnectionState = ConnectionState.ClosedDown;
        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                connectionState: invalidConnectionState);

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));
        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ConnectionStateValidationRule.MeteringPointConnectionStateError);
    }

    [Fact]
    public async Task Given_InvalidResolution_When_Validate_Then_ValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(Resolution.Daily.Name) // Daily resolution is invalid for ForwardMeteredData

            // Daily resolution requires the correct amount of measure data, else we also get other validation errors.
            .WithStartDateTime("2024-04-24T22:00:00Z")
            .WithEndDateTime("2024-04-25T22:00:00Z")
            .WithMeteredData([
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "1",
                    EnergyQuantity: "42",
                    QuantityQuality: null),
            ])
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(input);

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }

    [Fact]
    public async Task Given_InvalidMeteringPointType_When_Validate_Then_ValidationError()
    {
        const string invalidMeteringPoint = "InvalidMeteringPointType";
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointType(invalidMeteringPoint)
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                meteringPointType: MeteringPointType.Consumption); // Master data must have a valid metering point type

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));
        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointTypeValidationRule.WrongMeteringPointError);
    }

    [Fact]
    public async Task Given_IncorrectPositionCount_When_Validate_Then_ValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(Resolution.QuarterHourly.Name)
            .WithStartDateTime("2024-04-24T22:00:00Z")
            .WithEndDateTime("2024-04-25T02:00:00Z") // 4 hours should contain 4 * 4 = 16 positions
            .WithMeteredData([
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "1",
                    EnergyQuantity: "42",
                    QuantityQuality: null),
            ])
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(input);

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(PositionCountValidationRule.IncorrectNumberOfPositionsError(1, 16));
    }

    [Fact]
    public async Task Given_IncorrectQuality_When_Validate_Then_ValidationError()
    {
        const int periodLengthInHours = 4;
        var start = InstantPattern.General.Parse("2025-04-24T22:00:00Z").Value;

        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(Resolution.Hourly.Name)
            .WithStartDateTime(start.ToString())
            .WithEndDateTime(start.PlusHours(periodLengthInHours).ToString())
            .WithMeteredData(
                    Enumerable.Range(1, periodLengthInHours).Select(i =>
                    new ForwardMeteredDataInputV1.MeteredData(
                        Position: i.ToString(),
                        EnergyQuantity: "42",
                        QuantityQuality: "invalid-quality"))
                        .ToList())
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(input);

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeasureDataQualityValidationRule.InvalidQuality);
    }

    [Fact]
    public async Task Given_InvalidMeteringPointSubType_When_Validate_Then_ValidationError()
    {
        const MeteringPointSubType invalidMeteringPointSubType = MeteringPointSubType.Calculated;
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                meteringPointSubType: invalidMeteringPointSubType);

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));
        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointSubTypeValidationRule.WrongMeteringPointSubTypeError);
    }

    [Fact]
    public async Task Given_InvalidMeasurementUnit_When_Validate_Then_ValidationError()
    {
        const string invalidMeasurementUnit = "InvalidMeasurementUnit";

        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeasureUnit(invalidMeasurementUnit)
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                measurementUnit: MeasurementUnit.KilowattHour); // Master data must have a valid metering point type

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    meteringPointMasterData,
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeasurementUnitValidationRule.MeasurementUnitError);
    }
}
