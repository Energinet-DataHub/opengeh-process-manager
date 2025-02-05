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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_026.V1.BusinessValidation.Rules;

[SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Async suffix is not needed for test methods")]
public class EnergySupplierValidatorTest
{
    public const string ValidGlnNumber = "qwertyuiopasd"; // Must be 13 characters to be a valid GLN
    private const string ValidEicNumber = "qwertyuiopasdfgh"; // Must be 16 characters to be a valid GLN

    private static readonly ValidationError _invalidEnergySupplier = new("Feltet EnergySupplier skal være udfyldt med et valid GLN/EIC nummer når en elleverandør anmoder om data / EnergySupplier must be submitted with a valid GLN/EIC number when an energy supplier requests data", "E16");
    private static readonly ValidationError _notEqualToRequestedBy = new("Elleverandør i besked stemmer ikke overenes med elleverandør i header / Energy supplier in message does not correspond with energy supplier in header", "E16");

    private readonly EnergySupplierValidationRule _sut = new();

    [Fact]
    public async Task Validate_WhenEnergySupplierAndEnergySupplierIsValidGlnNumber_ReturnsNoValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.EnergySupplier)
            .WithRequestedForActorNumber(ValidGlnNumber)
            .WithEnergySupplierNumber(ValidGlnNumber)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenEnergySupplierAndEnergySupplierIsValidEicNumber_ReturnsNoValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.EnergySupplier)
            .WithRequestedForActorNumber(ValidEicNumber)
            .WithEnergySupplierNumber(ValidEicNumber)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenEnergySupplierAndMissingEnergySupplier_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.EnergySupplier)
            .WithEnergySupplierNumber(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        using var assertionScope = new AssertionScope();
        var error = errors.First();
        error.Message.Should().Be(_invalidEnergySupplier.Message);
        error.ErrorCode.Should().Be(_invalidEnergySupplier.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenEnergySupplierAndEnergySupplierNotEqualRequestedById_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.EnergySupplier)
            .WithRequestedForActorNumber(ValidGlnNumber)
            .WithEnergySupplierNumber(ValidEicNumber)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        using var assertionScope = new AssertionScope();
        var error = errors.Single();
        error.Message.Should().Be(_notEqualToRequestedBy.Message);
        error.ErrorCode.Should().Be(_notEqualToRequestedBy.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenEnergySupplierAndInvalidFormatEnergySupplier_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.EnergySupplier)
            .WithEnergySupplierNumber("invalid-format")
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        using var assertionScope = new AssertionScope();
        var error = errors.Single();
        error.Message.Should().Be(_invalidEnergySupplier.Message);
        error.ErrorCode.Should().Be(_invalidEnergySupplier.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenNotEnergySupplierAndMissingEnergySupplier_ReturnsNoValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithEnergySupplierNumber(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenNotEnergySupplierAndInvalidEnergySupplierFormat_ReturnsNoValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithEnergySupplierNumber("invalid-format")
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_IsNotEnergySupplierAndEnergySupplierNotEqualRequestedById_ReturnsNoValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithRequestedForActorNumber(ValidGlnNumber)
            .WithEnergySupplierNumber(ValidEicNumber)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }
}
