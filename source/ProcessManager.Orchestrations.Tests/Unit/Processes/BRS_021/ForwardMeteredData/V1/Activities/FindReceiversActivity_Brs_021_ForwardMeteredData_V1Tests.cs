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
using Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;
using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Text;
using MeteringPointType = Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects.MeteringPointType;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.Activities;

public class FindReceiversActivity_Brs_021_ForwardMeteredData_V1Tests
{
    private readonly OrchestrationInstanceId _orchestrationInstanceId = new(Guid.Empty);

    public FindReceiversActivity_Brs_021_ForwardMeteredData_V1Tests()
    {
        var clock = new Mock<IClock>();
        var orchestrationInstance = CreateOrchestrationInstance();
        var orchestrationInstanceProgressRepositoryMock = new Mock<IOrchestrationInstanceProgressRepository>();
        orchestrationInstanceProgressRepositoryMock
            .Setup(x => x.GetAsync(_orchestrationInstanceId))
            .ReturnsAsync(orchestrationInstance);
        orchestrationInstanceProgressRepositoryMock
            .Setup(x => x.UnitOfWork.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ElectricityMarketViewsMock = new Mock<IElectricityMarketViews>();
        Sut = new FindReceiversActivity_Brs_021_ForwardMeteredData_V1(
            clock.Object,
            orchestrationInstanceProgressRepositoryMock.Object,
            ElectricityMarketViewsMock.Object);
    }

    private Mock<IElectricityMarketViews> ElectricityMarketViewsMock { get; }

    private FindReceiversActivity_Brs_021_ForwardMeteredData_V1 Sut { get; }

    [Fact]
    public async Task MeasurementsMeteredData_WhenMeteringPointTypeIsConsumption_ReturnsExpectedReceivers()
    {
        // Arrange
        var expectedEnergySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);
        var expectedDanishEnergyAgency = (ActorId: "5798000020016", ActorRole: ActorRole.DanishEnergyAgency);

        var meteringPointType = MeteringPointType.Consumption;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var electricityMarketMeteringPointMasterData =
            MockElectricityMarketMeteringPointMasterDataToReturnActorId(
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
                electricityMarketMeteringPointMasterData));

        // Assert
        result.Should().HaveCount(2);
        result.Should()
            .ContainSingle(
                x => x.ActorId == expectedEnergySupplier.ActorId
                     && x.ActorRole == expectedEnergySupplier.ActorRole);
        result.Should()
            .ContainSingle(
                x => x.ActorId == expectedDanishEnergyAgency.ActorId
                     && x.ActorRole == expectedDanishEnergyAgency.ActorRole);
    }

    [Fact]
    public async Task MeasurementsMeteredData_WhenMeteringPointTypeIsProduction_ReturnsExpectedReceivers()
    {
        // Arrange
        var expectedEnergySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);
        var expectedDanishEnergyAgency = (ActorId: "5798000020016", ActorRole: ActorRole.DanishEnergyAgency);

        var meteringPointType = MeteringPointType.Production;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var electricityMarketMeteringPointMasterData =
            MockElectricityMarketMeteringPointMasterDataToReturnActorId(
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
                electricityMarketMeteringPointMasterData));

        // Assert
        result.Should().HaveCount(2);
        result.Should()
            .ContainSingle(
                x => x.ActorId == expectedEnergySupplier.ActorId
                     && x.ActorRole == expectedEnergySupplier.ActorRole);
        result.Should()
            .ContainSingle(
                x => x.ActorId == expectedDanishEnergyAgency.ActorId
                     && x.ActorRole == expectedDanishEnergyAgency.ActorRole);
    }

    [Fact]
    public async Task MeasurementsMeteredData_WhenMeteringPointTypeIsExchange_ReturnsExpectedReceivers()
    {
        // Arrange
        var expectedNeighborGridAreaOwner = (ActorId: "5798000020000", ActorRole: ActorRole.GridAccessProvider);

        var meteringPointType = MeteringPointType.Exchange;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var electricityMarketMeteringPointMasterData =
            MockElectricityMarketMeteringPointMasterDataToReturnActorId(
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
                electricityMarketMeteringPointMasterData));

        // Assert
        result.Should().HaveCount(1);
        result.Should()
            .ContainSingle(
                x => x.ActorId == expectedNeighborGridAreaOwner.ActorId
                     && x.ActorRole == expectedNeighborGridAreaOwner.ActorRole);
    }

    [Fact]
    public async Task MeasurementsMeteredData_WhenMeteringPointTypeIsVeProduction_ReturnsExpectedReceivers()
    {
        // Arrange
        var expectedParentEnergySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);
        var expectedDanishEnergyAgency = (ActorId: "5798000020016", ActorRole: ActorRole.DanishEnergyAgency);

        var meteringPointType = MeteringPointType.VeProduction;
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;
        var childEnergySupplier = (ActorId: "6798000020100", ActorRole: ActorRole.EnergySupplier);

        var electricityMarketChildMeteringPointMasterData =
            MockElectricityMarketMeteringPointMasterDataToReturnActorId(
                energySupplierId: childEnergySupplier.ActorId,
                startDate: startDate,
                endDate: endDate,
                parentEnergySupplierId: expectedParentEnergySupplier.ActorId);

        // Act
        var result = await Sut.Run(
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                _orchestrationInstanceId,
                meteringPointType.Code,
                startDate.ToString(),
                endDate.ToString(),
                electricityMarketChildMeteringPointMasterData));

        // Assert
        result.Should().HaveCount(2);
        result.Should()
            .ContainSingle(
                x => x.ActorId == expectedParentEnergySupplier.ActorId
                     && x.ActorRole == expectedParentEnergySupplier.ActorRole);
        result.Should()
            .ContainSingle(
                x => x.ActorId == expectedDanishEnergyAgency.ActorId
                     && x.ActorRole == expectedDanishEnergyAgency.ActorRole);
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
    //[InlineData("D14")]
    //[InlineData("D15")]
    //[InlineData("D19")]
    [InlineData("D13")]
    [InlineData("D17")]
    [InlineData("D18")]
    [InlineData("D20")]
    [InlineData("D21")]
    [InlineData("D22")]
    public async Task MeasurementsMeteredData_WhenMeteringPointTypeIsASupportedChildMeteringPoint_ReturnsExpectedReceivers(
        string meteringPointCode)
    {
        // Arrange
        var expectedEnergySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);

        var meteringPointType = MeteringPointType.FromCode(meteringPointCode);
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var electricityMarketChildMeteringPointMasterData =
            MockElectricityMarketMeteringPointMasterDataToReturnActorId(
                energySupplierId: expectedEnergySupplier.ActorId,
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
        result.Should().HaveCount(1);
        result.Should()
            .ContainSingle(
                x => x.ActorId == expectedEnergySupplier.ActorId
                     && x.ActorRole == expectedEnergySupplier.ActorRole);
    }

    [Theory]
    [InlineData("D14")]
    [InlineData("D15")]
    [InlineData("D19")]
    public async Task MeasurementsMeteredData_WhenMeteringPointTypeIsAUnsupportedChildMeteringPoint_ReturnsNoReceiver(
        string meteringPointCode)
    {
        // Arrange
        var expectedEnergySupplier = (ActorId: "5798000020000", ActorRole: ActorRole.EnergySupplier);

        var meteringPointType = MeteringPointType.FromCode(meteringPointCode);
        var startDate = InstantPattern.General.Parse("2025-01-01T00:00:00Z").Value;
        var endDate = InstantPattern.General.Parse("2025-01-02T00:00:00Z").Value;

        var electricityMarketChildMeteringPointMasterData =
            MockElectricityMarketMeteringPointMasterDataToReturnActorId(
                energySupplierId: expectedEnergySupplier.ActorId,
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
        result.Should().HaveCount(0);
    }

    private static OrchestrationInstance CreateOrchestrationInstance()
    {
        var userIdentity = new UserIdentity(
            new UserId(Guid.NewGuid()),
            new ActorId(Guid.NewGuid()));
        var orchestrationDescription = CreateOrchestrationDescription();
        return OrchestrationInstance.CreateFromDescription(
            userIdentity,
            orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
    }

    private static OrchestrationDescription CreateOrchestrationDescription()
    {
        var orchestrationDescription = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName("TestOrchestration", 4),
            canBeScheduled: true,
            functionName: "TestOrchestrationFunction");

        //orchestrationDescription.ParameterDefinition.SetFromType<TestOrchestrationParameter>();

        orchestrationDescription.AppendStepDescription("Test step 1");
        orchestrationDescription.AppendStepDescription("Test step 2");
        orchestrationDescription.AppendStepDescription("Test step 3");

        return orchestrationDescription;
    }

    private MeteringPointMasterData MockElectricityMarketMeteringPointMasterDataToReturnActorId(
        string energySupplierId,
        Instant startDate,
        Instant endDate,
        string? neighborGridAreaOwnerActorId = null,
        string? parentEnergySupplierId = null)
    {
        var electricityMarketMeteringPointMasterData = new MeteringPointMasterData();
        var electricityMarketMeteringPointEnergySupplier = new MeteringPointEnergySupplier();
        var electricityMarketActorNumberMock = new Mock<ActorNumber>(energySupplierId);
        // Reflection is used to set the private property of sealed MeteringPointEnergySupplier
        var energySupplierProperty = typeof(MeteringPointEnergySupplier).GetProperty(
            "EnergySupplier",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        energySupplierProperty!.SetValue(
            electricityMarketMeteringPointEnergySupplier,
            electricityMarketActorNumberMock.Object);

        if (neighborGridAreaOwnerActorId is not null)
        {
            var neighborGridAreaOwnerActorNumberMock = new Mock<ActorNumber>(neighborGridAreaOwnerActorId);

            // Use reflection to set the internal property
            var neighborGridAreaOwnersProperty = typeof(MeteringPointMasterData).GetProperty(
                "NeighborGridAreaOwners",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            neighborGridAreaOwnersProperty!.SetValue(
                electricityMarketMeteringPointMasterData,
                new List<ActorNumber>() { neighborGridAreaOwnerActorNumberMock.Object, });
        }

        ElectricityMarketViewsMock
            .Setup(
                x => x.GetMeteringPointEnergySuppliersAsync(
                    electricityMarketMeteringPointMasterData.Identification,
                    new Interval(startDate, endDate)))
            .Returns(
                new List<MeteringPointEnergySupplier> { electricityMarketMeteringPointEnergySupplier }
                    .ToAsyncEnumerable());

        if (parentEnergySupplierId is not null)
        {
            var electricityMarketMeteringPointParentEnergySupplier = new MeteringPointEnergySupplier();
            var electricityMarketParentEnergySupplierActorNumberMock = new Mock<ActorNumber>(parentEnergySupplierId);
            // Reflection is used to set the private property of sealed MeteringPointEnergySupplier
            var parentEnergySupplierProperty = typeof(MeteringPointEnergySupplier).GetProperty(
                "EnergySupplier",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            parentEnergySupplierProperty!.SetValue(
                electricityMarketMeteringPointParentEnergySupplier,
                electricityMarketParentEnergySupplierActorNumberMock.Object);

            var parentIdentification = typeof(MeteringPointMasterData).GetProperty(
                "ParentIdentification",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            parentIdentification!.SetValue(
                electricityMarketMeteringPointMasterData,
                new MeteringPointIdentification("Parent-id"));

            ElectricityMarketViewsMock
                .Setup(
                    x => x.GetMeteringPointEnergySuppliersAsync(
                        electricityMarketMeteringPointMasterData.ParentIdentification!,
                        new Interval(startDate, endDate)))
                .Returns(
                    new List<MeteringPointEnergySupplier> { electricityMarketMeteringPointParentEnergySupplier }
                        .ToAsyncEnumerable());
        }

        return electricityMarketMeteringPointMasterData;
    }
}
