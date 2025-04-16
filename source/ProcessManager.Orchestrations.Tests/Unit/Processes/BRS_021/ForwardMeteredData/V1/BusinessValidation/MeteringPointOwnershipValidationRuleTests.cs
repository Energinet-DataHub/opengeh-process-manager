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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.
    BusinessValidation;

public class MeteringPointOwnershipValidationRuleTests
{
    private readonly MeteringPointOwnershipValidationRule _sut = new();

    [Fact]
    public async Task Given_NoMasterData_When_ValidateAsync_Then_NoError()
    {
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                new ForwardMeteredDataInputV1Builder().Build(),
                null,
                []));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MasterDataWithAtLeastOneWrongOwner_When_ValidateAsync_Then_Error()
    {
        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId("1"),
            ValidFrom: DateTimeOffset.Now,
            ValidTo: DateTimeOffset.Now,
            GridAreaCode: new GridAreaCode("1"),
            GridAccessProvider: ActorNumber.Create("8888888888888"),
            NeighborGridAreaOwners: [],
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.Consumption,
            MeteringPointSubType: MeteringPointSubType.Physical,
            Resolution: Resolution.Hourly,
            MeasurementUnit: MeasurementUnit.KilowattHour,
            ProductId: "1",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111111"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                new ForwardMeteredDataInputV1Builder().WithGridAccessProviderNumber("9999999999999").Build(),
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
                    new MeteringPointMasterData(
                        MeteringPointId: new MeteringPointId("1"),
                        ValidFrom: DateTimeOffset.Now,
                        ValidTo: DateTimeOffset.Now,
                        GridAreaCode: new GridAreaCode("1"),
                        GridAccessProvider: ActorNumber.Create("9999999999999"),
                        NeighborGridAreaOwners: [],
                        ConnectionState: ConnectionState.Connected,
                        MeteringPointType: MeteringPointType.Consumption,
                        MeteringPointSubType: MeteringPointSubType.Physical,
                        Resolution: Resolution.Hourly,
                        MeasurementUnit: MeasurementUnit.KilowattHour,
                        ProductId: "1",
                        ParentMeteringPointId: null,
                        EnergySupplier: ActorNumber.Create("1111111111111")),
                    meteringPointMasterData,
                ]));

        result.Should().Contain(MeteringPointOwnershipValidationRule.MeteringPointHasWrongOwnerError);
    }

    [Fact]
    public async Task Given_MasterDataWithCorrectOwner_When_ValidateAsync_Then_NoError()
    {
        var meteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId("1"),
            ValidFrom: DateTimeOffset.Now,
            ValidTo: DateTimeOffset.Now,
            GridAreaCode: new GridAreaCode("1"),
            GridAccessProvider: ActorNumber.Create("9999999999999"),
            NeighborGridAreaOwners: [],
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.Consumption,
            MeteringPointSubType: MeteringPointSubType.Physical,
            Resolution: Resolution.Hourly,
            MeasurementUnit: MeasurementUnit.KilowattHour,
            ProductId: "1",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111111"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                new ForwardMeteredDataInputV1Builder().WithGridAccessProviderNumber("9999999999999").Build(),
                CurrentMasterData: meteringPointMasterData,
                HistoricalMeteringPointMasterData: [
                    new MeteringPointMasterData(
                        MeteringPointId: new MeteringPointId("1"),
                        ValidFrom: DateTimeOffset.Now,
                        ValidTo: DateTimeOffset.Now,
                        GridAreaCode: new GridAreaCode("1"),
                        GridAccessProvider: ActorNumber.Create("9999999999999"),
                        NeighborGridAreaOwners: [],
                        ConnectionState: ConnectionState.Connected,
                        MeteringPointType: MeteringPointType.Consumption,
                        MeteringPointSubType: MeteringPointSubType.Physical,
                        Resolution: Resolution.Hourly,
                        MeasurementUnit: MeasurementUnit.KilowattHour,
                        ProductId: "1",
                        ParentMeteringPointId: null,
                        EnergySupplier: ActorNumber.Create("1111111111111")),
                    meteringPointMasterData,
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_CurrentMasterDataWithCorrectOwnerAndHistoricalMeteringPointMasterDataWithDifferentOwner_When_ValidateAsync_Then_NoError()
    {
        var currentMeteringPointMasterData = new MeteringPointMasterData(
            MeteringPointId: new MeteringPointId("1"),
            ValidFrom: DateTimeOffset.Now,
            ValidTo: DateTimeOffset.Now,
            GridAreaCode: new GridAreaCode("1"),
            GridAccessProvider: ActorNumber.Create("9999999999888"),
            NeighborGridAreaOwners: [],
            ConnectionState: ConnectionState.Connected,
            MeteringPointType: MeteringPointType.Consumption,
            MeteringPointSubType: MeteringPointSubType.Physical,
            Resolution: Resolution.Hourly,
            MeasurementUnit: MeasurementUnit.KilowattHour,
            ProductId: "1",
            ParentMeteringPointId: null,
            EnergySupplier: ActorNumber.Create("1111111111111"));
        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                new ForwardMeteredDataInputV1Builder().WithGridAccessProviderNumber("9999999999888").Build(),
                CurrentMasterData: currentMeteringPointMasterData,
                HistoricalMeteringPointMasterData: [
                    new MeteringPointMasterData(
                        MeteringPointId: new MeteringPointId("1"),
                        ValidFrom: DateTimeOffset.Now,
                        ValidTo: DateTimeOffset.Now,
                        GridAreaCode: new GridAreaCode("1"),
                        GridAccessProvider: ActorNumber.Create("9999999999999"),
                        NeighborGridAreaOwners: [],
                        ConnectionState: ConnectionState.Connected,
                        MeteringPointType: MeteringPointType.Consumption,
                        MeteringPointSubType: MeteringPointSubType.Physical,
                        Resolution: Resolution.Hourly,
                        MeasurementUnit: MeasurementUnit.KilowattHour,
                        ProductId: "1",
                        ParentMeteringPointId: null,
                        EnergySupplier: ActorNumber.Create("1111111111111"))
                ]));

        result.Should().BeEmpty();
    }
}
