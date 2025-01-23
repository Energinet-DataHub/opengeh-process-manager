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

using System.Reflection;
using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Text;
using ActorNumber = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.ActorNumber;
using ActorRole = Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.ActorRole;
using ConnectionState = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.ConnectionState;
using GridAreaCode = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.GridAreaCode;
using MeteringPointMasterData = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointMasterData;
using MeteringPointSubType = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointSubType;
using MeteringPointType = Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects.MeteringPointType;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.Activities;

public class FindReceiversActivity_Brs_021_ForwardMeteredData_V1Tests
{
    private readonly OrchestrationInstanceId _orchestrationInstanceId = new(Guid.Empty);

    public FindReceiversActivity_Brs_021_ForwardMeteredData_V1Tests()
    {
        ElectricityMarketViewsMock = new Mock<IElectricityMarketViews>();
        Sut = new FindReceiversActivity_Brs_021_ForwardMeteredData_V1(
            ElectricityMarketViewsMock.Object);
    }

    private Mock<IElectricityMarketViews> ElectricityMarketViewsMock { get; }

    private FindReceiversActivity_Brs_021_ForwardMeteredData_V1 Sut { get; }

    [Fact]
    public async Task FindReceiversActivity_WhenMeteringPointTypeIsConsumption_ReturnsExpectedReceivers()
    {
        // Arrange
        var expectedEnergySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);
        var expectedDanishEnergyAgency = (ActorId: "5798000020016", ActorRole: ActorRole.DanishEnergyAgency);

        var meteringPointType = MeteringPointType.Consumption;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var meteringPointMasterData =
            SetupMeteringPointMasterData(
                expectedEnergySupplier.ActorId,
                startDate,
                endDate);

        // Act
        var result = await Sut.Run(
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                _orchestrationInstanceId,
                meteringPointType.Code,
                startDate.ToString(),
                endDate.ToString(),
                meteringPointMasterData));

        // Assert
        result.MarketActorRecipients.Should().HaveCount(2);
        result.MarketActorRecipients.Should()
            .ContainSingle(
                x => x.ActorId == expectedEnergySupplier.ActorId
                     && x.ActorRole == expectedEnergySupplier.ActorRole);
        result.MarketActorRecipients.Should()
            .ContainSingle(
                x => x.ActorId == expectedDanishEnergyAgency.ActorId
                     && x.ActorRole == expectedDanishEnergyAgency.ActorRole);
    }

    [Fact]
    public async Task FindReceiversActivity_WhenMeteringPointTypeIsProduction_ReturnsExpectedReceivers()
    {
        // Arrange
        var expectedEnergySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);
        var expectedDanishEnergyAgency = (ActorId: "5798000020016", ActorRole: ActorRole.DanishEnergyAgency);

        var meteringPointType = MeteringPointType.Production;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var meteringPointMasterData =
            SetupMeteringPointMasterData(
                expectedEnergySupplier.ActorId,
                startDate,
                endDate);

        // Act
        var result = await Sut.Run(
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                _orchestrationInstanceId,
                meteringPointType.Code,
                startDate.ToString(),
                endDate.ToString(),
                meteringPointMasterData));

        // Assert
        result.MarketActorRecipients.Should().HaveCount(2);
        result.MarketActorRecipients.Should()
            .ContainSingle(
                x => x.ActorId == expectedEnergySupplier.ActorId
                     && x.ActorRole == expectedEnergySupplier.ActorRole);
        result.MarketActorRecipients.Should()
            .ContainSingle(
                x => x.ActorId == expectedDanishEnergyAgency.ActorId
                     && x.ActorRole == expectedDanishEnergyAgency.ActorRole);
    }

    [Fact]
    public async Task FindReceiversActivity_WhenMeteringPointTypeIsExchange_ReturnsExpectedReceivers()
    {
        // Arrange
        var expectedNeighborGridAreaOwner = (ActorId: "5798000020000", ActorRole: ActorRole.GridAccessProvider);

        var meteringPointType = MeteringPointType.Exchange;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var meteringPointMasterData =
            SetupMeteringPointMasterData(
                "5798000020050",
                startDate,
                endDate,
                expectedNeighborGridAreaOwner.ActorId);

        // Act
        var result = await Sut.Run(
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                _orchestrationInstanceId,
                meteringPointType.Code,
                startDate.ToString(),
                endDate.ToString(),
                meteringPointMasterData));

        // Assert
        result.MarketActorRecipients.Should().HaveCount(1);
        result.MarketActorRecipients.Should()
            .ContainSingle(
                x => x.ActorId == expectedNeighborGridAreaOwner.ActorId
                     && x.ActorRole == expectedNeighborGridAreaOwner.ActorRole);
    }

    [Fact]
    public async Task FindReceiversActivity_WhenMeteringPointTypeIsVeProduction_ReturnsExpectedReceivers()
    {
        // Arrange
        var expectedSystemOperatorAgency = (ActorId: "5790000432752", ActorRole: ActorRole.SystemOperator);

        var meteringPointType = MeteringPointType.VeProduction;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;
        var childEnergySupplier = (ActorId: "6798000020100", ActorRole: ActorRole.EnergySupplier);

        var electricityMarketChildMeteringPointMasterData =
            SetupMeteringPointMasterData(
                energySupplierId: childEnergySupplier.ActorId,
                startDate: startDate,
                endDate: endDate);

        // Act
        var result = await Sut.Run(
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                _orchestrationInstanceId,
                meteringPointType.Code,
                startDate.ToString(),
                endDate.ToString(),
                electricityMarketChildMeteringPointMasterData));

        // Assert
        result.MarketActorRecipients.Should().HaveCount(1);
        result.MarketActorRecipients.Should()
            .ContainSingle(
                x => x.ActorId == expectedSystemOperatorAgency.ActorId
                     && x.ActorRole == expectedSystemOperatorAgency.ActorRole);
    }

    [Theory]
    [InlineData("D02")]
    [InlineData("D03")]
    [InlineData("D04")]
    [InlineData("D05")]
    [InlineData("D06")]
    [InlineData("D07")]
    [InlineData("D08")]
    [InlineData("D09")]
    [InlineData("D10")]
    [InlineData("D11")]
    [InlineData("D12")]
    [InlineData("D13")]
    [InlineData("D17")]
    [InlineData("D18")]
    [InlineData("D20")]
    [InlineData("D21")]
    [InlineData("D22")]
    public async Task FindReceiversActivity_WhenMeteringPointTypeIsASupportedChildMeteringPoint_ReturnsExpectedReceivers(
        string meteringPointCode)
    {
        // Arrange
        var expectedEnergySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);

        var meteringPointType = MeteringPointType.FromCode(meteringPointCode);
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;
        var childEnergySupplier = (ActorId: "6798000020100", ActorRole: ActorRole.EnergySupplier);

        var electricityMarketChildMeteringPointMasterData =
            SetupMeteringPointMasterData(
                energySupplierId: childEnergySupplier.ActorId,
                startDate: startDate,
                endDate: endDate,
                parentEnergySupplierId: expectedEnergySupplier.ActorId);

        // Act
        var result = await Sut.Run(
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                _orchestrationInstanceId,
                meteringPointType.Code,
                startDate.ToString(),
                endDate.ToString(),
                electricityMarketChildMeteringPointMasterData));

        // Assert
        result.MarketActorRecipients.Should().HaveCount(1);
        result.MarketActorRecipients.Should()
            .ContainSingle(
                x => x.ActorId == expectedEnergySupplier.ActorId
                     && x.ActorRole == expectedEnergySupplier.ActorRole);
    }

    [Fact]
    public async Task FindReceiversActivity_WhenMeteringPointTypeIsAnalysisAndParentMeteringPointDoesNotExists_ReturnsNoReceiver()
    {
        // Arrange
        var meteringPointType = MeteringPointType.Analysis;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;
        var energySupplier = (ActorId: "6798000020100", ActorRole: ActorRole.EnergySupplier);

        var electricityMarketChildMeteringPointMasterData =
            SetupMeteringPointMasterData(
                energySupplierId: energySupplier.ActorId,
                startDate: startDate,
                endDate: endDate);

        // Act
        var result = await Sut.Run(
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                _orchestrationInstanceId,
                meteringPointType.Code,
                startDate.ToString(),
                endDate.ToString(),
                electricityMarketChildMeteringPointMasterData));

        // Assert
        result.MarketActorRecipients.Should().BeEmpty();
    }

    [Theory]
    [InlineData("D14")]
    [InlineData("D15")]
    [InlineData("D19")]
    public async Task FindReceiversActivity_WhenMeteringPointTypeIsAUnsupportedChildMeteringPoint_ReturnsNoReceiver(
        string meteringPointCode)
    {
        // Arrange
        var energySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);

        var meteringPointType = MeteringPointType.FromCode(meteringPointCode);
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var electricityMarketChildMeteringPointMasterData =
            SetupMeteringPointMasterData(
                energySupplierId: energySupplier.ActorId,
                startDate: startDate,
                endDate: endDate);

        // Act
        var result = await Sut.Run(
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                _orchestrationInstanceId,
                meteringPointType.Code,
                startDate.ToString(),
                endDate.ToString(),
                electricityMarketChildMeteringPointMasterData));

        // Assert
        result.MarketActorRecipients.Should().HaveCount(0);
    }

    private static void SetProperty(object obj, string propertyName, object value)
    {
        var property = obj.GetType()
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (property != null && property.CanWrite)
        {
            property.SetValue(obj, value);
        }
    }

    private MeteringPointMasterData SetupMeteringPointMasterData(
        string energySupplierId,
        Instant startDate,
        Instant endDate,
        string? neighborGridAreaOwnerActorId = null,
        string? parentEnergySupplierId = null)
    {
        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId("StubId"),
            GridAreaCode: new GridAreaCode("StubGridAreaCode"),
            GridAccessProvider: new ActorNumber("StubGridAccessProvider"),
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.Production,
            MeteringPointSubType: MeteringPointSubType.Physical,
            MeasurementUnit: MeasurementUnit.Kilowatt,
            ParentMeteringPointId: parentEnergySupplierId == null ? null : new MeteringPointId("Parent_StubId"),
            NeighborGridAreaOwners: neighborGridAreaOwnerActorId == null ? new List<ActorNumber>() : new List<ActorNumber> { new(neighborGridAreaOwnerActorId) });

        var electricityMarketMeteringPointEnergySupplier = new MeteringPointEnergySupplier();
        var electricityMarketActorNumberMock = new Mock<Energinet.DataHub.ElectricityMarket.Integration.ActorNumber>(energySupplierId);
        SetProperty(electricityMarketMeteringPointEnergySupplier, "EnergySupplier", electricityMarketActorNumberMock.Object);

        ElectricityMarketViewsMock
            .Setup(
                x => x.GetMeteringPointEnergySuppliersAsync(
                    new MeteringPointIdentification(meteringPointMasterData.MeteringPointId.Value),
                    new Interval(startDate, endDate)))
            .Returns(
                new List<MeteringPointEnergySupplier> { electricityMarketMeteringPointEnergySupplier }
                    .ToAsyncEnumerable());

        if (parentEnergySupplierId is not null)
        {
            var electricityMarketMeteringPointParentEnergySupplier = new MeteringPointEnergySupplier();
            var electricityMarketParentEnergySupplierActorNumberMock = new Mock<Energinet.DataHub.ElectricityMarket.Integration.ActorNumber>(parentEnergySupplierId);
            SetProperty(electricityMarketMeteringPointParentEnergySupplier, "EnergySupplier", electricityMarketParentEnergySupplierActorNumberMock.Object);

            ElectricityMarketViewsMock
                .Setup(
                    x => x.GetMeteringPointEnergySuppliersAsync(
                        new MeteringPointIdentification(meteringPointMasterData.ParentMeteringPointId!.Value),
                        new Interval(startDate, endDate)))
                .Returns(
                    new List<MeteringPointEnergySupplier> { electricityMarketMeteringPointParentEnergySupplier }
                        .ToAsyncEnumerable());
        }

        return meteringPointMasterData;
    }
}
