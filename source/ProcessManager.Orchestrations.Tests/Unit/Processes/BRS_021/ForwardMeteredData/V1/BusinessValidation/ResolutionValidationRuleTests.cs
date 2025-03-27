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
using Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.FeatureFlags;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using Microsoft.FeatureManagement;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class ResolutionValidationRuleTests
{
    private readonly ResolutionValidationRule _sut = new();

    [Fact]
    public async Task Given_Validate_When_NoMasterData_Then_NoValidationError()
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                []));

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("QuarterHourly")]
    [InlineData("Hourly")]
    [InlineData("Monthly")]
    public async Task Given_Validate_When_ResolutionIsValid_Then_NoValidationError(string resolution)
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(resolution)
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
                        ConnectionState.Connected,
                        MeteringPointType.Production,
                        MeteringPointSubType.Physical,
                        Resolution.FromName(resolution),
                        MeasurementUnit.KilowattHour,
                        "product",
                        null,
                        ActorNumber.Create("1111111111112")),
                ]));

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Daily")]
    public async Task Given_Validate_When_ResolutionIsInvalid_Then_ValidationError(string resolution)
    {
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(resolution)
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
                    ConnectionState.Connected,
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    Resolution.FromName(resolution),
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }

    [Fact]
    public async Task Given_Validate_When_MasterDataHasTwoDifferentResolutions_Then_ValidationError()
    {
        var validResolution = Resolution.QuarterHourly;
        var input = new ForwardMeteredDataInputV1Builder()
            .WithResolution(validResolution.Name)
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
                    ConnectionState.Connected,
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    validResolution,
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
                    // Different resolution
                    Resolution.Hourly,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }

    [Fact]
    public async Task Given_Validate_When_MasterDataMeteringPointIsNotProductionAndResolutionIsMonthly_Then_ValidationError()
    {
        var validResolutionForProduction = Resolution.Monthly;
        var input = new ForwardMeteredDataInputV1Builder()
            .WithMeteringPointType(MeteringPointType.Production.Name)
            .WithResolution(validResolutionForProduction.Name)
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
                    ConnectionState.Connected,
                    // Not production metering point type
                    MeteringPointType.Consumption,
                    MeteringPointSubType.Physical,
                    validResolutionForProduction,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }

    [Fact]
    public async Task Given_Validate_WhenInputIsNotProductionAndResolutionIsMonthly_Then_ValidationError()
    {
        var validResolutionForProduction = Resolution.Monthly;
        var input = new ForwardMeteredDataInputV1Builder()
            // Not production metering point type
            .WithMeteringPointType(MeteringPointType.Consumption.Name)
            .WithResolution(validResolutionForProduction.Name)
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
                    ConnectionState.Connected,
                    MeteringPointType.Production,
                    MeteringPointSubType.Physical,
                    validResolutionForProduction,
                    MeasurementUnit.KilowattHour,
                    "product",
                    null,
                    ActorNumber.Create("1111111111112")),
            ]));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(ResolutionValidationRule.WrongResolutionError);
    }
}
