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

using FluentAssertions;
using Google.Protobuf;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Measurements.Contracts;

public class PersistSubmittedTransactionTests
{
    [Fact]
    public void PersistSubmittedTransaction_Equality_ShouldBeEqual()
    {
        // Arrange
        var testTransaction = new Energinet.DataHub.Measurements.Tests.Contracts.PersistSubmittedTransaction
        {
            OrchestrationInstanceId = "instance-id",
            OrchestrationType = Energinet.DataHub.Measurements.Tests.Contracts.OrchestrationType.OtSubmittedMeasureData,
            MeteringPointId = "metering-point-id",
            TransactionId = "transaction-id",
            TransactionCreationDatetime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
            StartDatetime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow.AddHours(-1)),
            EndDatetime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
            MeteringPointType = Energinet.DataHub.Measurements.Tests.Contracts.MeteringPointType.MptConsumption,
            Product = "product",
            Unit = Energinet.DataHub.Measurements.Tests.Contracts.Unit.UKwh,
            Resolution = Energinet.DataHub.Measurements.Tests.Contracts.Resolution.RPt15M,
            Points = { new Energinet.DataHub.Measurements.Tests.Contracts.Point { Position = 1, Quantity = new Energinet.DataHub.Measurements.Tests.Contracts.DecimalValue { Units = 10, Nanos = 0 }, Quality = Energinet.DataHub.Measurements.Tests.Contracts.Quality.QMeasured } },
        };

        var expectedTransaction = new Energinet.DataHub.Measurements.Contracts.PersistSubmittedTransaction
        {
            OrchestrationInstanceId = testTransaction.OrchestrationInstanceId,
            OrchestrationType = Energinet.DataHub.Measurements.Contracts.OrchestrationType.OtSubmittedMeasureData,
            MeteringPointId = testTransaction.MeteringPointId,
            TransactionId = testTransaction.TransactionId,
            TransactionCreationDatetime = testTransaction.TransactionCreationDatetime,
            StartDatetime = testTransaction.StartDatetime,
            EndDatetime = testTransaction.EndDatetime,
            MeteringPointType = Energinet.DataHub.Measurements.Contracts.MeteringPointType.MptConsumption,
            Product = testTransaction.Product,
            Unit = Energinet.DataHub.Measurements.Contracts.Unit.UKwh,
            Resolution = Energinet.DataHub.Measurements.Contracts.Resolution.RPt15M,
            Points = { new Energinet.DataHub.Measurements.Contracts.Point { Position = 1, Quantity = new Energinet.DataHub.Measurements.Contracts.DecimalValue { Units = 10, Nanos = 0 }, Quality = Energinet.DataHub.Measurements.Contracts.Quality.QMeasured } },
        };

        // Act
        var serializedData = testTransaction.ToByteArray();
        var expectedSerializedData = expectedTransaction.ToByteArray();

        // Assert
        serializedData.Should().BeEquivalentTo(expectedSerializedData);
    }

    [Fact]
    public void OrchestrationType_Equality_ShouldBeEqual()
    {
        // Arrange & Act
        var testOrchestrationTypes = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Tests.Contracts.OrchestrationType));
        var expectedOrchestrationTypes = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Contracts.OrchestrationType));

        // Assert
        testOrchestrationTypes.Should().BeEquivalentTo(expectedOrchestrationTypes);
    }

    [Fact]
    public void Quality_Equality_ShouldBeEqual()
    {
        // Arrange & Act
        var testQualityValues = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Tests.Contracts.Quality));
        var expectedQualityValues = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Contracts.Quality));

        // Assert
        testQualityValues.Should().BeEquivalentTo(expectedQualityValues);
    }

    [Fact]
    public void MeteringPointType_Equality_ShouldBeEqual()
    {
        // Arrange & Act
        var testMeteringPointTypes = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Tests.Contracts.MeteringPointType));
        var expectedMeteringPointTypes = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Contracts.MeteringPointType));

        // Assert
        testMeteringPointTypes.Should().BeEquivalentTo(expectedMeteringPointTypes);
    }

    [Fact]
    public void Unit_Equality_ShouldBeEqual()
    {
        // Arrange & Act
        var testUnitValues = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Tests.Contracts.Unit));
        var expectedUnitValues = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Contracts.Unit));

        // Assert
        testUnitValues.Should().BeEquivalentTo(expectedUnitValues);
    }

    [Fact]
    public void Resolution_Equality_ShouldBeEqual()
    {
        // Arrange & Act
        var testResolutionValues = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Tests.Contracts.Resolution));
        var expectedResolutionValues = Enum.GetValues(typeof(Energinet.DataHub.Measurements.Contracts.Resolution));

        // Assert
        testResolutionValues.Should().BeEquivalentTo(expectedResolutionValues);
    }
}
