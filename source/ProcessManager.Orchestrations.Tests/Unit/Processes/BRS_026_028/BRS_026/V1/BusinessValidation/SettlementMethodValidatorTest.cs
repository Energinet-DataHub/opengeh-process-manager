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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;

public class SettlementMethodValidatorTest
{
    private static readonly ValidationError _invalidSettlementMethod = new("SettlementMethod kan kun benyttes i kombination med E17 og skal være enten D01 og E02 / SettlementMethod can only be used in combination with E17 and must be either D01 or E02", "D15");

    private readonly SettlementMethodValidationRule _sut = new();

    [Theory]
    [InlineData(nameof(SettlementMethod.Flex))]
    [InlineData(nameof(SettlementMethod.NonProfiled))]
    public async Task Validate_WhenConsumptionAndSettlementMethodIsValid_ReturnsNoValidationErrorsAsync(string settlementMethod)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.EnergySupplier)
            .WithMeteringPointType(MeteringPointType.Consumption.Name)
            .WithSettlementMethod(settlementMethod)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(MeteringPointType.Production))]
    [InlineData(nameof(MeteringPointType.Exchange))]
    [InlineData("not-consumption")]
    public async Task Validate_WhenMeteringPointTypeIsGivenAndSettlementMethodIsNull_ReturnsNoValidationErrorsAsync(string meteringPointType)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.EnergySupplier)
            .WithMeteringPointType(meteringPointType)
            .WithSettlementMethod(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenConsumptionAndSettlementMethodIsInvalid_ReturnsExpectedValidationErrorAsync()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.EnergySupplier)
            .WithMeteringPointType(MeteringPointType.Consumption.Name)
            .WithSettlementMethod("invalid-settlement-method")
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        var error = errors.First();
        error.ErrorCode.Should().Be(_invalidSettlementMethod.ErrorCode);
        error.Message.Should().Be(_invalidSettlementMethod.Message);
    }

    [Theory]
    [InlineData(nameof(MeteringPointType.Production), nameof(SettlementMethod.Flex))]
    [InlineData(nameof(MeteringPointType.Production), nameof(SettlementMethod.NonProfiled))]
    [InlineData(nameof(MeteringPointType.Production), "invalid-settlement-method")]
    [InlineData(nameof(MeteringPointType.Exchange), nameof(SettlementMethod.Flex))]
    [InlineData(nameof(MeteringPointType.Exchange), nameof(SettlementMethod.NonProfiled))]
    [InlineData(nameof(MeteringPointType.Exchange), "invalid-settlement-method")]
    [InlineData("not-consumption-metering-point", nameof(SettlementMethod.Flex))]
    [InlineData("not-consumption-metering-point", nameof(SettlementMethod.NonProfiled))]
    [InlineData("not-consumption-metering-point", "invalid-settlement-method")]
    [InlineData("", nameof(SettlementMethod.Flex))]
    [InlineData("", nameof(SettlementMethod.NonProfiled))]
    [InlineData("", "invalid-settlement-method")]
    [InlineData(null, nameof(SettlementMethod.Flex))]
    [InlineData(null, nameof(SettlementMethod.NonProfiled))]
    [InlineData(null, "invalid-settlement-method")]
    public async Task Validate_WhenNotConsumptionAndSettlementMethodIsGiven_ReturnsExpectedValidationErrorAsync(string? meteringPointType, string settlementMethod)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.EnergySupplier)
            .WithMeteringPointType(meteringPointType)
            .WithSettlementMethod(settlementMethod)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        var error = errors.First();
        error.ErrorCode.Should().Be(_invalidSettlementMethod.ErrorCode);
        error.Message.Should().Be(_invalidSettlementMethod.Message);
    }
}
