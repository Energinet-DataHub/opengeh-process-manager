﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using FluentAssertions;
using NodaTime;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class ConnectionStateValidationRuleTests
{
    private readonly ConnectionStateValidationRule _sut = new();

    [Theory]
    [InlineData(ConnectionState.NotUsed)]
    [InlineData(ConnectionState.ClosedDown)]
    [InlineData(ConnectionState.New)]
    public async Task Given_ValidateMeteringPointMasterData_When_ConnectionStateIsInvalid_Then_ValidationError(ConnectionState connectionState)
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(new(
            input,
            [
            new MeteringPointMasterData(
                new MeteringPointId("id"),
                SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                new GridAreaCode("111"),
                ActorNumber.Create("1111111111111"),
                [],
                connectionState,
                MeteringPointType.Production,
                MeteringPointSubType.Physical,
                Resolution.Hourly,
                MeasurementUnit.KilowattHour,
                "product",
                null,
                ActorNumber.Create("1111111111112")),
            new MeteringPointMasterData(
                new MeteringPointId("id"),
                SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                new GridAreaCode("111"),
                ActorNumber.Create("1111111111111"),
                [],
                ConnectionState.Connected,
                MeteringPointType.Production,
                MeteringPointSubType.Physical,
                Resolution.Hourly,
                MeasurementUnit.KilowattHour,
                "product",
                null,
                ActorNumber.Create("1111111111112")),
        ]));

        result.Should()
            .ContainSingle()
            .And.Contain(
                ve => ve.ErrorCode == "D16"
                      && ve.Message == "Målepunktet skal have status tilsluttet eller afbrudt/meteringpoint must have status connected or disconnected");
    }

    [Theory]
    [InlineData(ConnectionState.Connected)]
    [InlineData(ConnectionState.Disconnected)]
    public async Task Given_ValidateMeteringPointMasterData_When_ConnectionStateIsValid_Then_NoValidationError(ConnectionState connectionState)
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("id"),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset(),
                        new GridAreaCode("111"),
                        ActorNumber.Create("1111111111111"),
                        [],
                        connectionState,
                        MeteringPointType.Production,
                        MeteringPointSubType.Physical,
                        Resolution.Hourly,
                        MeasurementUnit.KilowattHour,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ValidateMeteringPointMasterData_When_NoMasterData_Then_NoValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                []));

        result.Should().BeEmpty();
    }
}
