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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_026.V1.BusinessValidation.Rules;

public class MeteringPointTypeValidatorTests
{
    private static readonly ValidationError _invalidMeteringPointType =
        new(
            "Metering point type skal være en af følgende: {PropertyName} eller undladt / Metering point type has one of the following: {PropertyName} or omitted",
            "D18");

    private readonly MeteringPointTypeValidationRule _sut = new();

    [Theory]
    [InlineData(nameof(MeteringPointType.Consumption))]
    [InlineData(nameof(MeteringPointType.Production))]
    [InlineData(nameof(MeteringPointType.Exchange))]
    [InlineData(null)]
    public async Task Validate_WhenMeteringPointIsValid_ReturnsExpectedNoValidationErrors(string? meteringPointType)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.MeteredDataResponsible)
            .WithMeteringPointType(meteringPointType)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Validate_WhenMeteringPointTypeIsInvalid_ReturnsExpectedValidationError(string? meteringPointType)
    {
        // Arrange
        var message = new RequestCalculatedEnergyTimeSeriesInputV1Builder(
                ActorRole.MeteredDataResponsible)
            .WithMeteringPointType(meteringPointType)
            .Build();

        // Act
        var errors = await _sut.ValidateAsync(message);

        // Assert
        errors.Should().ContainSingle();
        var error = errors.First();
        error.ErrorCode.Should().Be(_invalidMeteringPointType.ErrorCode);
        error.Message.Should().Be(_invalidMeteringPointType.WithPropertyName("E17, E18, E20").Message);
    }
}
