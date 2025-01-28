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
using AutoFixture.Xunit2;
using Energinet.DataHub.Core.TestCommon.AutoFixture.Attributes;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation.Rules;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026.V1.BusinessValidation.Rules;

[SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Async suffix is not needed for test methods")]
public class GridAreaValidatorTest
{
    private const string ValidGlnNumber = "qwertyuiopasd"; // Must be 13 characters to be a valid GLN
    private static readonly ValidationError _missingGridAreaCode = new("Netområde er obligatorisk for rollen MDR / Grid area is mandatory for the role MDR.", "D64");
    private static readonly ValidationError _invalidGridArea = new("Ugyldig netområde / Invalid gridarea", "E86");

    [Theory]
    [InlineAutoMoqData]
    public async Task Validate_WhenRequesterIsGridOwnerOfRequestedGridArea_ReturnsNoValidationErrors(
        [Frozen] Mock<IGridAreaOwnerClient> gridAreaOwnerClient,
        GridAreaValidationRule sut)
    {
        // Arrange
        var gridAreaCode = "123";
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.MeteredDataResponsible)
            .WithGridArea(gridAreaCode)
            .Build();

        gridAreaOwnerClient.Setup(repo => repo.IsCurrentOwnerAsync(
                gridAreaCode,
                message.RequestedForActorNumber,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var errors = await sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineAutoMoqData]
    public async Task Validate_WhenRequesterIsNotGridOwnerOfRequestedGridArea_ReturnsExpectedValidationError(
        [Frozen] Mock<IGridAreaOwnerClient> gridAreaOwnerClient,
        GridAreaValidationRule sut)
    {
        // Arrange
        var gridAreaCode = "123";

        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.MeteredDataResponsible)
            .WithGridArea(gridAreaCode)
            .Build();

        gridAreaOwnerClient.Setup(repo => repo.IsCurrentOwnerAsync(
                gridAreaCode,
                message.RequestedForActorNumber,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var errors = await sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        using var assertionScope = new AssertionScope();
        var error = errors.Single();
        error.Message.Should().Be(_invalidGridArea.Message);
        error.ErrorCode.Should().Be(_invalidGridArea.ErrorCode);
    }

    [Theory]
    [InlineAutoMoqData]
    public async Task Validate_WhenGridAreaCodeIsEmpty_ReturnsExpectedValidationError(
        GridAreaValidationRule sut)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.MeteredDataResponsible)
            .WithGridArea(null)
            .Build();

        // Act
        var errors = await sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();

        using var assertionScope = new AssertionScope();
        var error = errors.Single();
        error.Message.Should().Be(_missingGridAreaCode.Message);
        error.ErrorCode.Should().Be(_missingGridAreaCode.ErrorCode);
    }
}
