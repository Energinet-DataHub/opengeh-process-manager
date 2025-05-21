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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.BusinessValidation;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.SendMeasurements.V1.BusinessValidation;

public class QuantityValidationRuleTests
{
    private readonly QuantityValidationRule _sut = new();

    [Fact]
    public async Task Given_GoodValidQuantities_When_ValidateAsync_Then_NoError()
    {
        var meteredData = new List<ForwardMeteredDataInputV1.MeteredData>
        {
            CreateMeteredData(position: 1, quantity: "0.123"),
            CreateMeteredData(position: 2, quantity: "0.000"),
            CreateMeteredData(position: 3, quantity: "1234567890"),
            CreateMeteredData(position: 4, quantity: "1234567890.123"),
            CreateMeteredData(position: 5, quantity: null),
            CreateMeteredData(position: 6, quantity: "  2  "), // the spaces must be there.
        };

        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithMeteredData(meteredData)
                    .Build(),
                []));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_NegativeQuantity_When_ValidateAsync_Then_Error()
    {
        var meteredData = new List<ForwardMeteredDataInputV1.MeteredData>
        {
            CreateMeteredData(position: 1, quantity: "-1"),
        };

        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithMeteredData(meteredData)
                    .Build(),
                []));

        result.Should().ContainSingle()
            .And.Contain(QuantityValidationRule.QuantityMustBePositive.WithPropertyName("1"));
    }

    [Fact]
    public async Task Given_QuantityWith4Decimals_When_ValidateAsync_Then_Error()
    {
        var meteredData = new List<ForwardMeteredDataInputV1.MeteredData>
        {
            CreateMeteredData(position: 1, quantity: "0.1234"),
        };

        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithMeteredData(meteredData)
                    .Build(),
                []));

        result.Should().ContainSingle()
            .And.Contain(QuantityValidationRule.MaxNumberOfDecimals.WithPropertyName("1"));
    }

    [Fact]
    public async Task Given_QuantityWith11Integers_When_ValidateAsync_Then_Error()
    {
        var meteredData = new List<ForwardMeteredDataInputV1.MeteredData>
        {
            CreateMeteredData(position: 1, quantity: "12345678901"),
        };

        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithMeteredData(meteredData)
                    .Build(),
                []));

        result.Should().ContainSingle()
            .And.Contain(QuantityValidationRule.MaxNumberOfIntegers.WithPropertyName("1"));
    }

    [Fact]
    public async Task Given_AllQuantityErrors_when_ValidateAsync_Then_Errors()
    {
        var meteredData = new List<ForwardMeteredDataInputV1.MeteredData>
        {
            CreateMeteredData(position: 1, quantity: "12345678901.0003"), // 11 integers and 4 decimals
            CreateMeteredData(position: 2, quantity: "asd"), // Not a decimal
            CreateMeteredData(position: 3, quantity: "-3.0004"), // Negative and 4 decimals
        };

        var result = await _sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithMeteredData(meteredData)
                    .Build(),
                []));

        result.Should().HaveCount(5)
            .And.BeEquivalentTo([
                QuantityValidationRule.MaxNumberOfIntegers.WithPropertyName("1"),
                QuantityValidationRule.MaxNumberOfDecimals.WithPropertyName("1"),
                QuantityValidationRule.QuantityMustBePositive.WithPropertyName("2"),
                QuantityValidationRule.QuantityMustBePositive.WithPropertyName("3"),
                QuantityValidationRule.MaxNumberOfDecimals.WithPropertyName("3")
                ]);
    }

    private ForwardMeteredDataInputV1.MeteredData CreateMeteredData(int position, string? quantity)
    {
        return new(
            EnergyQuantity: quantity,
            QuantityQuality: Quality.AsProvided.Name,
            Position: position.ToString());
    }
}
