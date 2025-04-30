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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using FluentAssertions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeasurements.V1.BusinessValidation;

public class MeasurementUnitValidationRuleTests
{
    private readonly MeasurementUnitValidationRule _sut = new();

    public static TheoryData<MeasurementUnit> ValidMeasurementUnits => new()
    {
        MeasurementUnit.KilowattHour,
        MeasurementUnit.KiloVoltAmpereReactiveHour,
    };

    public static TheoryData<MeasurementUnit> InvalidMeasurementUnits => new()
    {
        MeasurementUnit.Ampere,
        MeasurementUnit.Pieces,
        MeasurementUnit.Kilowatt,
        MeasurementUnit.Megawatt,
        MeasurementUnit.MegawattHour,
        MeasurementUnit.MetricTon,
        MeasurementUnit.MegaVoltAmpereReactivePower,
        MeasurementUnit.DanishTariffCode,
    };

    [Fact]
    public async Task Given_NoMasterData_When_Validate_Then_NoValidationError()
    {
        var input = new ForwardMeasurementsInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                []));

        result.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ValidMeasurementUnits))]
    public async Task Given_ValidMeasurementUnits_When_Validate_Then_NoValidationError(MeasurementUnit measurementUnit)
    {
        var input = new ForwardMeasurementsInputV1Builder()
            .WithMeasureUnit(measurementUnit.Name)
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("id"),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        new GridAreaCode("111"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Production,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        measurementUnit,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidMeasurementUnits))]
    public async Task Given_InvalidMeasurementUnits_When_Validate_Then_ValidationError(MeasurementUnit measurementUnit)
    {
        var input = new ForwardMeasurementsInputV1Builder()
            .WithMeasureUnit(measurementUnit.Name)
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("id"),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        new GridAreaCode("111"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        ConnectionState.Connected,
                        MeteringPointType.Production,
                        MeteringPointSubType.Physical,
                        Resolution.QuarterHourly,
                        measurementUnit,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeasurementUnitValidationRule.MeasurementUnitError);
    }

    [Fact]
    public async Task Given_MultipleMasterDataWhereMeasurementUnitDoesntMatchOneOfThem_When_Validate_Then_ValidationError()
    {
        var input = new ForwardMeasurementsInputV1Builder()
            .WithMeasureUnit(MeasurementUnit.KilowattHour.Name)
            .Build();

        var result = await _sut.ValidateAsync(new(
            input,
            [
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    Resolution.QuarterHourly,
                    // Correct MeasurementUnit
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
                new MeteringPointMasterData(
                    new MeteringPointId("id"),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                    new GridAreaCode("111"),
                    ActorNumber.Create("1111111111111"),
                    [],
                    ConnectionState.Connected,
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    Resolution.QuarterHourly,
                    // Incorrect MeasurementUnit
                    MeasurementUnit.KiloVoltAmpereReactiveHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeasurementUnitValidationRule.MeasurementUnitNotAllowedError);
    }
}
