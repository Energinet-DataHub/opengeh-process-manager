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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.BusinessValidation;
using FluentAssertions;
using NodaTime;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.SendMeasurements.V1.BusinessValidation;

public class MeteringPointSubTypeValidationRuleTests
{
    private readonly MeteringPointSubTypeValidationRule _sut = new();

    public static TheoryData<MeteringPointSubType> ValidMeteringPointSubTypes => new()
    {
        MeteringPointSubType.Physical,
        MeteringPointSubType.Virtual,
    };

    public static TheoryData<MeteringPointSubType> InvalidMeteringPointSubTypes => new()
    {
        MeteringPointSubType.Calculated,
    };

    [Fact]
    public async Task Given_NoMasterData_When_Validate_Then_NoValidationError()
    {
        var input = new SendMeasurementsInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                []));

        result.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ValidMeteringPointSubTypes))]
    public async Task Given_ValidMeteringPointSubTypes_When_Validate_Then_NoValidationError(MeteringPointSubType meteringPointSubType)
    {
        // Metering point subtype is not part of the input, its only part of the master data
        var input = new SendMeasurementsInputV1Builder()
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
                        meteringPointSubType,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidMeteringPointSubTypes))]
    public async Task Given_InvalidMeteringPointSubType_When_Validate_Then_NoValidationError(MeteringPointSubType meteringPointSubType)
    {
        // Metering point subtype is not part of the input, its only part of the master data
        var input = new SendMeasurementsInputV1Builder()
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
                        meteringPointSubType,
                        Resolution.QuarterHourly,
                        MeasurementUnit.KilowattHour,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointSubTypeValidationRule.WrongMeteringPointSubTypeError);
    }

    [Fact]
    public async Task Given_DifferentMeteringPointSubTypeFromMasterData_When_Validate_Then_ValidationError()
    {
        var input = new SendMeasurementsInputV1Builder()
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
                    // One MeteringPointSubType
                    MeteringPointSubType.Physical,
                    Resolution.QuarterHourly,
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
                    // A different MeteringPointSubType
                    MeteringPointSubType.Virtual,
                    Resolution.QuarterHourly,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointSubTypeValidationRule.WrongMeteringPointSubTypeError);
    }
}
