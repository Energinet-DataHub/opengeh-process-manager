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

using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ElectricityMarket.Integration.Models.Common;
using Energinet.DataHub.ElectricityMarket.Integration.Models.ProcessDelegation;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Moq;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;

public class DelegationProviderTests
{
    [Fact]
    public async Task Given_ActorRoleNotDelegatedOrGridAccessProvider_When_GetDelegationAsync_Then_IsNotDelegated()
    {
        // Arrange
        var mockElectricityMarketViews = new Mock<IElectricityMarketViews>();
        var delegationProvider = new DelegationProvider(mockElectricityMarketViews.Object);

        var actorNumber = ActorNumber.Create("1234567890123");
        var actorRole = ActorRole.MeteredDataResponsible; // Not Delegated or GridAccessProvider
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            actorNumber,
            actorRole,
            gridAreaCode);

        // Assert
        Assert.Null(result.ActorNumber);
        Assert.False(result.IsDelegated);
    }

    [Fact]
    public async Task Given_ActorRoleDelegated_When_GetDelegationAsync_Then_IsDelegatedToActor()
    {
        // Arrange
        var mockElectricityMarketViews = new Mock<IElectricityMarketViews>();
        var delegation = new ProcessDelegationDto("4567890123456", EicFunction.Delegated);
        mockElectricityMarketViews
            .Setup(x => x.GetProcessDelegationAsync(
                "1234567890123",
                EicFunction.Delegated,
                "123",
                DelegatedProcess.ReceiveMeteringPointData))
            .ReturnsAsync(delegation);

        var delegationProvider = new DelegationProvider(mockElectricityMarketViews.Object);

        var actorNumber = ActorNumber.Create("1234567890123");
        var actorRole = ActorRole.Delegated;
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            actorNumber,
            actorRole,
            gridAreaCode);

        // Assert
        Assert.Equal("4567890123456", result.ActorNumber);
        Assert.True(result.IsDelegated);
    }

    [Fact]
    public async Task Given_ActorRoleGridAccessProvider_When_GetDelegationAsync_Then_IsDelegatedToActor()
    {
        // Arrange
        var mockElectricityMarketViews = new Mock<IElectricityMarketViews>();
        var delegation = new ProcessDelegationDto("4567890123456", EicFunction.GridAccessProvider);
        mockElectricityMarketViews
            .Setup(x => x.GetProcessDelegationAsync(
                "1234567890123",
                EicFunction.GridAccessProvider,
                "123",
                DelegatedProcess.ReceiveMeteringPointData))
            .ReturnsAsync(delegation);

        var delegationProvider = new DelegationProvider(mockElectricityMarketViews.Object);

        var actorNumber = ActorNumber.Create("1234567890123");
        var actorRole = ActorRole.GridAccessProvider;
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            actorNumber,
            actorRole,
            gridAreaCode);

        // Assert
        Assert.Equal("4567890123456", result.ActorNumber);
        Assert.True(result.IsDelegated);
    }

    [Fact]
    public async Task Given_NoDelegationAndActorRoleGridAccessProvider_When_GetDelegationAsync_Then_IsNotDelegated()
    {
        // Arrange
        var mockElectricityMarketViews = new Mock<IElectricityMarketViews>();
        mockElectricityMarketViews
            .Setup(x => x.GetProcessDelegationAsync(
                "1234567890123",
                EicFunction.GridAccessProvider,
                "123",
                DelegatedProcess.ReceiveMeteringPointData))
            .ReturnsAsync(null as ProcessDelegationDto);

        var delegationProvider = new DelegationProvider(mockElectricityMarketViews.Object);

        var actorNumber = ActorNumber.Create("1234567890123");
        var actorRole = ActorRole.GridAccessProvider;
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            actorNumber,
            actorRole,
            gridAreaCode);

        // Assert
        Assert.Null(result.ActorNumber);
        Assert.False(result.IsDelegated);
    }

    [Fact]
    public async Task Given_NoDelegationAndActorRoleDelegated_When_GetDelegationAsync_Then_IsDelegationAndNoActorNumber()
    {
        // Arrange
        var mockElectricityMarketViews = new Mock<IElectricityMarketViews>();
        mockElectricityMarketViews
            .Setup(x => x.GetProcessDelegationAsync(
                "1234567890123",
                EicFunction.Delegated,
                "123",
                DelegatedProcess.ReceiveMeteringPointData))
            .ReturnsAsync(null as ProcessDelegationDto);

        var delegationProvider = new DelegationProvider(mockElectricityMarketViews.Object);

        var actorNumber = ActorNumber.Create("1234567890123");
        var actorRole = ActorRole.Delegated;
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            actorNumber,
            actorRole,
            gridAreaCode);

        // Assert
        Assert.Null(result.ActorNumber);
        Assert.True(result.IsDelegated);
    }
}
