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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Moq;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.MeteringPointMasterData;

public class DelegationProviderTests
{
    [Fact]
    public async Task Given_ActorRoleNotDelegatedOrGridAccessProvider_When_GetDelegationAsync_Then_ShouldNotBeDelegated()
    {
        // Arrange
        var mockElectricityMarketViews = new Mock<IElectricityMarketViews>();
        var delegationProvider = new DelegationProvider(mockElectricityMarketViews.Object);

        var gridAreaOwner = ActorNumber.Create("9874567890123");
        var senderActorNumber = ActorNumber.Create("1234567890123");
        var senderActorRole = ActorRole.MeteredDataResponsible; // Not Delegated or GridAccessProvider
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            gridAreaOwner,
            gridAreaCode,
            senderActorNumber,
            senderActorRole);

        // Assert
        Assert.False(result.ShouldBeDelegated);
        Assert.Null(result.DelegatedFromActorNumber);
    }

    [Fact]
    public async Task Given_SenderActorRoleIsActorRoleDelegated_When_GetDelegationAsync_Then_ShouldBeDelegatedToActor()
    {
        // Arrange
        var gridAreaOwner = ActorNumber.Create("9874567890123");
        var mockElectricityMarketViews = new Mock<IElectricityMarketViews>();
        var delegation = new ProcessDelegationDto("1234567890123", EicFunction.Delegated);
        mockElectricityMarketViews
            .Setup(x => x.GetProcessDelegationAsync(
                gridAreaOwner.Value,
                EicFunction.GridAccessProvider,
                "123",
                DelegatedProcess.ReceiveMeteringPointData))
            .ReturnsAsync(delegation);

        var delegationProvider = new DelegationProvider(mockElectricityMarketViews.Object);
        var senderActorNumber = ActorNumber.Create("1234567890123");
        var senderActorRole = ActorRole.Delegated;
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            gridAreaOwner,
            gridAreaCode,
            senderActorNumber,
            senderActorRole);

        // Assert
        Assert.True(result.ShouldBeDelegated);
        Assert.Equal("9874567890123", result.DelegatedFromActorNumber);
    }

    [Fact]
    public async Task Given_SenderActorRoleIsGridAccessProvider_When_GetDelegationAsync_Then_ShouldBeDelegatedToActor()
    {
        // Arrange
        var gridAreaOwner = ActorNumber.Create("9874567890123");
        var mockElectricityMarketViews = new Mock<IElectricityMarketViews>();
        var delegation = new ProcessDelegationDto("1234567890123", EicFunction.GridAccessProvider);
        mockElectricityMarketViews
            .Setup(x => x.GetProcessDelegationAsync(
                gridAreaOwner.Value,
                EicFunction.GridAccessProvider,
                "123",
                DelegatedProcess.ReceiveMeteringPointData))
            .ReturnsAsync(delegation);

        var delegationProvider = new DelegationProvider(mockElectricityMarketViews.Object);
        var senderActorNumber = ActorNumber.Create("1234567890123");
        var senderActorRole = ActorRole.GridAccessProvider;
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            gridAreaOwner,
            gridAreaCode,
            senderActorNumber,
            senderActorRole);

        // Assert
        Assert.True(result.ShouldBeDelegated);
        Assert.Equal("9874567890123", result.DelegatedFromActorNumber);
    }

    [Fact]
    public async Task Given_NoDelegationAndActorRoleGridAccessProvider_When_GetDelegationAsync_Then_ShouldNotBeDelegated()
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

        var gridAreaOwner = ActorNumber.Create("9874567890123");
        var senderActorNumber = ActorNumber.Create("1234567890123");
        var senderActorRole = ActorRole.GridAccessProvider;
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            gridAreaOwner,
            gridAreaCode,
            senderActorNumber,
            senderActorRole);

        // Assert
        Assert.False(result.ShouldBeDelegated);
        Assert.Null(result.DelegatedFromActorNumber);
    }

    [Fact]
    public async Task Given_NoDelegationAndActorRoleDelegated_When_GetDelegationAsync_Then_ShouldBeDelegatedAndToActorNotFound()
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

        var gridAreaOwner = ActorNumber.Create("9874567890123");
        var senderActorNumber = ActorNumber.Create("1234567890123");
        var senderActorRole = ActorRole.Delegated;
        var gridAreaCode = new GridAreaCode("123");

        // Act
        var result = await delegationProvider.GetDelegatedFromAsync(
            gridAreaOwner,
            gridAreaCode,
            senderActorNumber,
            senderActorRole);

        // Assert
        Assert.Null(result.DelegatedFromActorNumber);
        Assert.True(result.ShouldBeDelegated);
    }
}
