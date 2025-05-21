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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Model;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.Model;

public class ForwardMeteredDataValidInputTests
{
    [Fact]
    public void Given_NoProductNumber_When_From_Then_ExpectedProductNumberIsSet()
    {
        // Arrange
        const string expectedProductNumber = "8716867000030";
        var builder = new ForwardMeteredDataInputV1Builder();

        var input = builder
            .WithProductNumber(null)
            .Build();

        // Act
        var sut = ForwardMeteredDataValidInput.From(input);

        // Assert
        sut.ProductNumber.Should().Be(expectedProductNumber);
    }

    [Fact]
    public void Given_NoQualityAndQuantity_When_From_Then_ThenExpectedQuantityQualityIsSet()
    {
        // Arrange
        var expectedQuantityQuality = Quality.AsProvided;
        var builder = new ForwardMeteredDataInputV1Builder();

        var meteredDataWithNoQualityAndQuantity = new ForwardMeteredDataInputV1.MeteredData("0", null, null);
        var input = builder.WithMeteredData([meteredDataWithNoQualityAndQuantity]).Build();

        // Act
        var sut = ForwardMeteredDataValidInput.From(input);

        // Assert
        sut.Measurements.Should().Contain(m =>
            m.Position == 0 &&
            m.EnergyQuantity == null &&
            m.QuantityQuality == expectedQuantityQuality);
    }
}
