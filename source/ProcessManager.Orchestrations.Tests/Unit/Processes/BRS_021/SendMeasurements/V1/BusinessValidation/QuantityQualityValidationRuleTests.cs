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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.SendMeasurements.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.SendMeasurements.V1.BusinessValidation;

public class QuantityQualityValidationRuleTests
{
    private static readonly Quality[] _validQualities =
    [
        Quality.NotAvailable,
        Quality.Estimated,
        Quality.AsProvided,
    ];

    private readonly QuantityQualityValidationRule _sut = new();

    public static TheoryData<Quality?> ValidQualities =>
    [
        null, // Null means "AsProvided"
        .._validQualities,
    ];

    public static TheoryData<Quality?> InvalidQualities =>
    [
        ..EnumerationRecordType.GetAll<Quality>()
            .Except(_validQualities),
    ];

    [Theory]
    [MemberData(nameof(ValidQualities))]
    public async Task Given_OneMeasurement_AndGiven_ValidQuality_When_Validate_Then_NoValidationErrors(Quality? quality)
    {
        var input = new SendMeasurementsInputV1Builder()
            .WithMeteredData(
            [
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "1",
                    EnergyQuantity: "1",
                    QuantityQuality: quality?.Name),
            ])
            .Build();

        var result = await _sut.ValidateAsync(
            new SendMeasurementsBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [])); // Master data is unused in validation rule

        Assert.Empty(result);
    }

    [Fact]
    public async Task Given_MultipleMeasurements_AndGiven_ValidQualities_When_Validate_Then_NoValidationErrors()
    {
        // Input that contains more than one measurement with valid qualities
        var input = new SendMeasurementsInputV1Builder()
            .WithMeteredData(
            [
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "1",
                    EnergyQuantity: "42",
                    QuantityQuality: null), // Valid quality
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "2",
                    EnergyQuantity: "42",
                    QuantityQuality: Quality.NotAvailable.Name), // Another valid quality
            ])
            .Build();

        var result = await _sut.ValidateAsync(
            new SendMeasurementsBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [])); // Master data is unused in validation rule

        Assert.Empty(result);
    }

    [Theory]
    [MemberData(nameof(InvalidQualities))]
    public async Task Given_OneMeasurement_AndGiven_InvalidQuality_When_Validate_Then_ValidationErrors(Quality? quality)
    {
        var input = new SendMeasurementsInputV1Builder()
            .WithMeteredData(
            [
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "1",
                    EnergyQuantity: "42",
                    QuantityQuality: quality?.Name),
            ])
            .Build();

        var result = await _sut.ValidateAsync(
            new SendMeasurementsBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [])); // Master data is unused in validation rule

        var validationError = Assert.Single(result);
        Assert.Equal(QuantityQualityValidationRule.InvalidQuality.Single(), validationError);
    }

    [Fact]
    public async Task Given_MultipleMeasurements_AndGiven_OneInvalidQuality_When_Validate_Then_ValidationError()
    {
        var input = new SendMeasurementsInputV1Builder()
            .WithMeteredData(
            [
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "1",
                    EnergyQuantity: "42",
                    QuantityQuality: null), // Valid
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "2",
                    EnergyQuantity: "42",
                    QuantityQuality: null), // Valid
                new ForwardMeteredDataInputV1.MeteredData(
                    Position: "3",
                    EnergyQuantity: "42",
                    QuantityQuality: "invalid-quality"), // Invalid
            ])
            .Build();

        var result = await _sut.ValidateAsync(
            new SendMeasurementsBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [])); // Master data is unused in validation rule

        var validationError = Assert.Single(result);
        Assert.Equal(QuantityQualityValidationRule.InvalidQuality.Single(), validationError);
    }
}
