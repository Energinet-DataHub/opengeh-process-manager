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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.
    ElectricityMarket;

public class MeteringPointReceiversProviderTests
{
    private readonly MeteringPointReceiversProvider _sut = new();

    [Fact]
    public void Consumption()
    {
        var meteringPointMasterData = GetMasterData(MeteringPointType.Consumption);

        var result = _sut.GetReceiversFromMasterData(meteringPointMasterData);
        result.Actors.OrderBy(a => a.ActorRole.Name)
            .Should()
            .SatisfyRespectively(
                mar =>
                {
                    mar.ActorNumber.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    mar.ActorRole.Should().Be(ActorRole.DanishEnergyAgency);
                },
                mar =>
                {
                    mar.ActorNumber.Should().Be(meteringPointMasterData.EnergySupplier);
                    mar.ActorRole.Should().Be(ActorRole.EnergySupplier);
                });

        result.Actors.Should().HaveCount(2);
    }

    [Fact]
    public void Production()
    {
        var meteringPointMasterData = GetMasterData(MeteringPointType.Production);

        var result = _sut.GetReceiversFromMasterData(meteringPointMasterData);

        result.Actors.Should().HaveCount(2);
        result.Actors.OrderBy(a => a.ActorRole.Name)
            .Should()
            .SatisfyRespectively(
                mar =>
                {
                    mar.ActorNumber.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    mar.ActorRole.Should().Be(ActorRole.DanishEnergyAgency);
                },
                mar =>
                {
                    mar.ActorNumber.Should().Be(meteringPointMasterData.EnergySupplier);
                    mar.ActorRole.Should().Be(ActorRole.EnergySupplier);
                });
    }

    [Fact]
    public void Exchange()
    {
        var meteringPointMasterData = GetMasterData(MeteringPointType.Exchange);

        var result = _sut.GetReceiversFromMasterData(meteringPointMasterData);

        result.Actors.Should().HaveCount(2);
        result.Actors
            .OrderBy(a => a.ActorRole.Name)
            .ThenBy(a => a.ActorNumber.Value)
            .Should()
            .SatisfyRespectively(
                mar =>
                {
                    mar.ActorNumber.Should().Be(ActorNumber.Create("3333333333333"));
                    mar.ActorRole.Should().Be(ActorRole.GridAccessProvider);
                },
                mar =>
                {
                    mar.ActorNumber.Should().Be(ActorNumber.Create("4444444444444"));
                    mar.ActorRole.Should().Be(ActorRole.GridAccessProvider);
                });
    }

    [Fact]
    public void VeProduction()
    {
        var meteringPointMasterData = GetMasterData(MeteringPointType.VeProduction);

        var result = _sut.GetReceiversFromMasterData(meteringPointMasterData);

        result.Actors.Should().HaveCount(2);
        result.Actors
            .OrderBy(a => a.ActorRole.Name)
            .ThenBy(a => a.ActorNumber.Value)
            .Should()
            .SatisfyRespectively(
                mar =>
                {
                    mar.ActorNumber.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    mar.ActorRole.Should().Be(ActorRole.DanishEnergyAgency);
                },
                mar =>
                {
                    mar.ActorNumber.Value.Should().Be(DataHubDetails.SystemOperatorNumber);
                    mar.ActorRole.Should().Be(ActorRole.SystemOperator);
                });
    }

    [Theory]
    [InlineData(MeteringPointType.NetProduction)]
    [InlineData(MeteringPointType.SupplyToGrid)]
    [InlineData(MeteringPointType.ConsumptionFromGrid)]
    [InlineData(MeteringPointType.WholesaleServicesInformation)]
    [InlineData(MeteringPointType.OwnProduction)]
    [InlineData(MeteringPointType.NetFromGrid)]
    [InlineData(MeteringPointType.NetToGrid)]
    [InlineData(MeteringPointType.TotalConsumption)]
    [InlineData(MeteringPointType.Analysis)]
    [InlineData(MeteringPointType.NotUsed)]
    [InlineData(MeteringPointType.SurplusProductionGroup6)]
    [InlineData(MeteringPointType.NetLossCorrection)]
    [InlineData(MeteringPointType.OtherConsumption)]
    [InlineData(MeteringPointType.OtherProduction)]
    [InlineData(MeteringPointType.ExchangeReactiveEnergy)]
    [InlineData(MeteringPointType.CollectiveNetProduction)]
    [InlineData(MeteringPointType.CollectiveNetConsumption)]
    public void Think_of_the_children()
    {
        var meteringPointMasterData = GetMasterData(MeteringPointType.VeProduction);

        var result = _sut.GetReceiversFromMasterData(meteringPointMasterData);

        result.Actors.Should().HaveCount(2);
        result.Actors
            .OrderBy(a => a.ActorRole.Name)
            .ThenBy(a => a.ActorNumber.Value)
            .Should()
            .SatisfyRespectively(
                mar =>
                {
                    mar.ActorNumber.Value.Should().Be(DataHubDetails.DanishEnergyAgencyNumber);
                    mar.ActorRole.Should().Be(ActorRole.DanishEnergyAgency);
                },
                mar =>
                {
                    mar.ActorNumber.Value.Should().Be(DataHubDetails.SystemOperatorNumber);
                    mar.ActorRole.Should().Be(ActorRole.SystemOperator);
                });
    }

    private MeteringPointMasterData GetMasterData(MeteringPointType meteringPointType) =>
        new(
            new MeteringPointId("1"),
            new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new GridAreaCode("1"),
            ActorNumber.Create("1111111111111"),
            ["3333333333333", "4444444444444"],
            ConnectionState.Connected,
            meteringPointType,
            MeteringPointSubType.Physical,
            Resolution.QuarterHourly,
            MeasurementUnit.KilowattHour,
            "1",
            null,
            ActorNumber.Create("2222222222222"));
}
