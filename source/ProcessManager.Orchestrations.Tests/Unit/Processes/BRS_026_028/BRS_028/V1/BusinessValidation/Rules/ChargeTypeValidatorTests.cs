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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_028.V1.BusinessValidation.Rules;

public class ChargeTypeValidatorTests
{
    private static readonly ValidationError _chargeTypeIdIsToLongError = new(
        "Følgende chargeType mRID er for lang: {PropertyName}. Den må højst indeholde 10 karaktere/"
        + "The following chargeType mRID is to long: {PropertyName} It must at most be 10 characters",
        "D14");

    private readonly ChargeCodeValidationRule _sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("%/)(&)")]
    [InlineData("0000000000")]
    [InlineData("1234567890")]
    [InlineData("-234567890")]
    public async Task Validate_WhenChargeTypeContainsAValidType_returnsExpectedValidationError(string? chargeCode)
    {
        // Arrange
        var chargeTypes = Array.Empty<RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput>();

        if (chargeCode is not null)
        {
            chargeTypes = [
                new RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput(
                ChargeType: "D01",
                ChargeCode: chargeCode),
            ];
        }

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithChargeTypes(chargeTypes)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(message);

        // Assert
        validationErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("12345678901")] // 11 char long
    public async Task Validate_WhenChargeTypeContainsAInvalidType_returnsExpectedValidationError(string chargeCode)
    {
        // Arrange
        var chargeType = new RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput(
            ChargeType: "D01",
            ChargeCode: chargeCode);

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithChargeType(chargeType)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(message);

        // Assert
        validationErrors.Should().ContainSingle()
            .Subject.Should().Be(
                _chargeTypeIdIsToLongError.WithPropertyName(chargeCode));
    }

    [Fact]
    public async Task Validate_WhenChargeTypeContainsMultipleInvalidType_returnsExpectedValidationError()
    {
        // Arrange
        var chargeTypes = new RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput[]
            {
                new(ChargeType: "D01", ChargeCode: "12345678901"),
                new(ChargeType: "D01", ChargeCode: "10987654321"),
            };

        var expectedErrors = new List<ValidationError>();
        foreach (var t in chargeTypes)
        {
            expectedErrors.Add(_chargeTypeIdIsToLongError.WithPropertyName(t.ChargeCode!));
        }

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithChargeTypes(chargeTypes)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(message);

        // Assert
        validationErrors.Should().Equal(expectedErrors);
    }

    [Fact]
    public async Task Validate_WhenMultipleChargeTypeButOneHasInvalidType_returnsExpectedValidationError()
    {
        // Arrange
        const string invalidChargeCode = "ThisIsMoreThan10CharacterLong";
        var chargeTypes = new RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput[]
            {
                new(ChargeType: "D01", ChargeCode: "valid1"),
                new(ChargeType: "D01", ChargeCode: invalidChargeCode),
                new(ChargeType: "D01", ChargeCode: "valid2"),
            };

        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithChargeTypes(chargeTypes)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(message);

        // Assert
        validationErrors.Should().ContainSingle().Subject.Should().Be(_chargeTypeIdIsToLongError.WithPropertyName(invalidChargeCode));
    }
}
