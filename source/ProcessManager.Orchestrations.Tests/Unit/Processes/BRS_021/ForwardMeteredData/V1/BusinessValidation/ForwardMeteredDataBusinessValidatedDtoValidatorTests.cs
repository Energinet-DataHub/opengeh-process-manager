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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;

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
                .IsEnabledAsync(FeatureFlag.EnableBrs021ForwardMeteredDataBusinessValidationForMeteringPoint))
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

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.Hourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
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

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.Hourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
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
                CurrentMasterData: null,
                HistoricalMeteringPointMasterData: []));

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

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create("9999999999999"),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.Hourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
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

        var invalidConnectionState = ConnectionState.ClosedDown;
        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: invalidConnectionState,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.Hourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
                    meteringPointMasterData,
                ]));
        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ConnectionStateValidationRule.MeteringPointConnectionStateError);
    }

    [Fact]
    public async Task Given_InvalidResolution_When_Validate_Then_ValidationError()
    {
        var invalidResolution = Resolution.Daily;
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(invalidResolution.Name)
            .WithMeteredData(
                Enumerable.Range(1, 31)
                    .Select(
                        i => new ForwardMeteredDataInputV1.MeteredData(
                            Position: i.ToString(),
                            "1024",
                            Quality.AsProvided.Name))
                    .ToList())
            .Build();

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.Hourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
                    meteringPointMasterData,
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }

    [Fact]
    public async Task Given_InvalidMeteringPointType_When_Validate_Then_ValidationError()
    {
        var invalidMeteringPoint = "InvalidMeteringPointType";
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointType(invalidMeteringPoint)
            .Build();

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.ElectricalHeating,
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.Hourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
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
            .Build();

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.QuarterHourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
                    meteringPointMasterData,
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(PositionCountValidationRule.IncorrectNumberOfPositionsError(744, 2976));
    }

    [Fact]
    public async Task Given_InvalidMeteringPointSubType_When_Validate_Then_ValidationError()
    {
        var invalidMeteringPointSubType = MeteringPointSubType.Calculated;
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: invalidMeteringPointSubType,
            MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.Hourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
                    meteringPointMasterData,
                ]));
        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointSubTypeValidationRule.WrongMeteringPointSubTypeError);
    }

    [Fact]
    public async Task Given_InvalidMeasurementUnit_When_Validate_Then_ValidationError()
    {
        var invalidMeasurementUnit = "InvalidMeasurementUnit";
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeasureUnit(invalidMeasurementUnit)
            .Build();

        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            GridAreaCode: new GridAreaCode("804"),
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.KilowattHour,
            ValidFrom: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            ValidTo: SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
            NeighborGridAreaOwners: [],
            Resolution: Resolution.Hourly,
            ProductId: "product",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111112"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
                    meteringPointMasterData,
                ]));
        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeasurementUnitValidationRule.MeasurementUnitError);
    }
}
