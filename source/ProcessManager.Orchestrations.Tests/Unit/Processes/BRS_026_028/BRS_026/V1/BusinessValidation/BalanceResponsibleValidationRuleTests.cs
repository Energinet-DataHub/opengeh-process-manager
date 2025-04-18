﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;

[SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Async suffix is not needed for test methods")]
public class BalanceResponsibleValidationRuleTests
{
    public const string ValidGlnNumber = "qwertyuiopasd"; // Must be 13 characters to be a valid GLN
    private const string ValidEicNumber = "qwertyuiopasdfgh"; // Must be 16 characters to be a valid GLN
    private const string BalanceResponsibleRole = "BalanceResponsibleParty";
    private static readonly ValidationError _invalidBalanceResponsible = new("Feltet BalanceResponsibleParty skal være udfyldt med et valid GLN/EIC når en balanceansvarlig anmoder om data / BalanceResponsibleParty must be submitted with a valid GLN/EIC when a balance responsible requests data", "E18");
    private static readonly ValidationError _mismatchedBalanceResponsibleInHeaderAndMessage = new("Den balanceansvarlige i beskeden stemmer ikke overenes med den balanceansvarlige i headeren / BalanceResponsibleParty in the message does not correspond with balance responsible in header", "E18");
    private static readonly ValidationError _invalidBusinessReason = new("En balanceansvarlig kan kun benytte forretningsårsag D03 eller D04 i forbindelse med en anmodning / A BalanceResponsibleParty can only use business reason D03 or D04 in connection with a request", "D11");

    private readonly BalanceResponsibleValidationRule _sut = new();

    [Fact]
    public async Task Validate_WhenRequesterIsBalanceResponsibleAndBalanceResponsibleFieldIsValidGlnNumber_ReturnsNoValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenRequesterIsBalanceResponsibleAndBalanceResponsibleFieldIsValidEicNumber_ReturnsNoValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithRequestedForActorNumber(ValidEicNumber)
            .WithBalanceResponsibleNumber(ValidEicNumber)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenRequesterIsBalanceResponsibleAndMissingBalanceResponsibleField_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithBalanceResponsibleNumber(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        using var assertionScope = new AssertionScope();
        var error = errors.Single();
        error.Message.Should().Be(_invalidBalanceResponsible.Message);
        error.ErrorCode.Should().Be(_invalidBalanceResponsible.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenRequesterIsBalanceResponsibleAndBalanceResponsibleFieldNotEqualRequestedById_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithRequestedForActorNumber(ValidGlnNumber)
            .WithBalanceResponsibleNumber(ValidEicNumber)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        var error = errors.Single();
        error.Message.Should().Be(_mismatchedBalanceResponsibleInHeaderAndMessage.Message);
        error.ErrorCode.Should().Be(_mismatchedBalanceResponsibleInHeaderAndMessage.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenRequesterIsBalanceResponsibleAndInvalidBalanceResponsibleField_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithBalanceResponsibleNumber("invalid-format")
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        using var assertionScope = new AssertionScope();
        var error = errors.Single();
        error.Message.Should().Be(_invalidBalanceResponsible.Message);
        error.ErrorCode.Should().Be(_invalidBalanceResponsible.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenRequesterIsNotBalanceResponsibleAndMissingBalanceResponsibleField_ReturnsNoValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.EnergySupplier)
            .WithBalanceResponsibleNumber(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenBusinessReasonIsBalanceFixing_ReturnsNoValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithBusinessReason(BusinessReason.BalanceFixing.Name)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenBusinessReasonIsPreliminaryAggregation_ReturnsNoValidationErrors()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithBusinessReason(BusinessReason.PreliminaryAggregation.Name)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenRequestingInvalidBusinessReason_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithBusinessReason(BusinessReason.WholesaleFixing.Name)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        using var assertionScope = new AssertionScope();
        var error = errors.Single();
        error.Message.Should().Be(_invalidBusinessReason.Message);
        error.ErrorCode.Should().Be(_invalidBusinessReason.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenRequestingInvalidBusinessReasonWithInvalidId_ReturnsExpectedValidationError()
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.BalanceResponsibleParty)
            .WithBalanceResponsibleNumber("invalid-format")
            .WithBusinessReason(BusinessReason.Correction.Name)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        using var assertionScope = new AssertionScope();
        errors.Should().ContainSingle(error => error.ErrorCode == _invalidBalanceResponsible.ErrorCode);
        errors.Should().ContainSingle(error => error.ErrorCode == _invalidBusinessReason.ErrorCode);
    }
}
