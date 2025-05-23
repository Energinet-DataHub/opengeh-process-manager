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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;

public class SettlementVersionValidationRuleTests
{
    private static readonly ValidationError _expectedInvalidSettlementMethodError = new("SettlementSeriesVersion kan kun benyttes i kombination med D32 og skal være enten D01, D02 eller D03 / SettlementSeriesVersion can only be used in combination with D32 and must be either D01, D02 or D03", "E86");

    private readonly SettlementVersionValidationRule _sut = new();

    [Theory]
    [InlineData("invalid-settlement-series-version")]
    [InlineData("D04")]
    [InlineData("")]
    public async Task Validate_WhenCorrectionAndInvalidSeriesVersion_ReturnsValidationErrorsAsync(string invalidSettlementVersion)
    {
        // Arrange
        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithBusinessReason(BusinessReason.Correction)
            .WithSettlementVersion(invalidSettlementVersion)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Be(_expectedInvalidSettlementMethodError);
    }

    [Theory]
    [InlineData("invalid-settlement-series-version")]
    [InlineData("D04")]
    [InlineData("")]
    [InlineData(nameof(SettlementVersion.FirstCorrection))]
    [InlineData(nameof(SettlementVersion.SecondCorrection))]
    [InlineData(nameof(SettlementVersion.ThirdCorrection))]
    public async Task Validate_WhenNotCorrectionAndSettlementVersionExists_ReturnsValidationErrorsAsync(string settlementVersion)
    {
        // Arrange
        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithBusinessReason(BusinessReason.WholesaleFixing)
            .WithSettlementVersion(settlementVersion)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Be(_expectedInvalidSettlementMethodError);
    }

    [Theory]
    [InlineData(nameof(SettlementVersion.FirstCorrection))]
    [InlineData(nameof(SettlementVersion.SecondCorrection))]
    [InlineData(nameof(SettlementVersion.ThirdCorrection))]
    public async Task Validate_WhenCorrectionAndValidSettlementVersion_ReturnsNoValidationErrorsAsync(string validSettlementVersion)
    {
        // Arrange
        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithBusinessReason(BusinessReason.Correction)
            .WithSettlementVersion(validSettlementVersion)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenCorrectionAndNoSettlementVersion_ReturnsNoValidationErrorsAsync()
    {
        // Arrange
        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithBusinessReason(BusinessReason.Correction)
            .WithSettlementVersion(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty("When Settlement version is empty the latest correction result is requested");
    }

    [Fact]
    public async Task Validate_WhenNotCorrectionAndNoSettlementVersion_ReturnsNoValidationErrorsAsync()
    {
        // Arrange
        var message = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithBusinessReason(BusinessReason.WholesaleFixing)
            .WithSettlementVersion(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }
}
