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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;

public class TimeSeriesTypeValidationRuleTests
{
    private static readonly ValidationError _invalidTimeSeriesTypeForActor = new("Den forespurgte tidsserie type kan ikke forespørges som en {PropertyName} / The requested time series type can not be requested as a {PropertyName}", "D11");

    private readonly TimeSeriesTypeValidationRule _sut = new();

    [Theory]
    [InlineData(nameof(MeteringPointType.Production), null)]
    [InlineData(nameof(MeteringPointType.Exchange), null)]
    [InlineData(nameof(MeteringPointType.Consumption), null)]
    [InlineData(nameof(MeteringPointType.Consumption), nameof(SettlementMethod.NonProfiled))]
    [InlineData(nameof(MeteringPointType.Consumption), nameof(SettlementMethod.Flex))]
    public async Task Validate_AsMeteredDataResponsible_ReturnsNoValidationErrors(string meteringPointType, string? settlementMethod)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.MeteredDataResponsible)
            .WithMeteringPointType(meteringPointType)
            .WithSettlementMethod(settlementMethod)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(MeteringPointType.Production), null)]
    [InlineData(nameof(MeteringPointType.Consumption), nameof(SettlementMethod.NonProfiled))]
    [InlineData(nameof(MeteringPointType.Consumption), nameof(SettlementMethod.Flex))]
    public async Task Validate_AsEnergySupplier_ReturnsNoValidationErrors(string meteringPointType, string? settlementMethod)
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
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(MeteringPointType.Production), null)]
    [InlineData(nameof(MeteringPointType.Consumption), nameof(SettlementMethod.NonProfiled))]
    [InlineData(nameof(MeteringPointType.Consumption), nameof(SettlementMethod.Flex))]
    public async Task Validate_AsBalanceResponsible_ReturnsNoValidationErrors(string meteringPointType, string? settlementMethod)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.BalanceResponsibleParty)
            .WithMeteringPointType(meteringPointType)
            .WithSettlementMethod(settlementMethod)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(MeteringPointType.Exchange))]
    [InlineData(nameof(MeteringPointType.Consumption))]
    public async Task Validate_AsEnergySupplierAndNoSettlementMethod_ReturnsExpectedValidationErrors(string meteringPointType)
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
        errors.Should().ContainSingle();

        var error = errors.First();
        error.Message.Should().Be(_invalidTimeSeriesTypeForActor.WithPropertyName(ActorRole.EnergySupplier.Name).Message);
        error.ErrorCode.Should().Be(_invalidTimeSeriesTypeForActor.ErrorCode);
    }

    [Theory]
    [InlineData(nameof(MeteringPointType.Exchange))]
    [InlineData(nameof(MeteringPointType.Consumption))]
    public async Task Validate_AsBalanceResponsibleAndNoSettlementMethod_ValidationErrors(string meteringPointType)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                forActorRole: ActorRole.BalanceResponsibleParty)
            .WithMeteringPointType(meteringPointType)
            .WithSettlementMethod(null)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        var error = errors.First();
        error.Message.Should().Be(_invalidTimeSeriesTypeForActor.WithPropertyName(ActorRole.BalanceResponsibleParty.Name).Message);
        error.ErrorCode.Should().Be(_invalidTimeSeriesTypeForActor.ErrorCode);
    }
}
