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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.BusinessValidation;
using FluentAssertions;
using NodaTime;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_024.V1.BusinessValidation;

public class MeteringPointTypeValidationRuleTests
{
    private readonly MeteringPointTypeValidationRule _sut = new();

    public static TheoryData<MeteringPointType> ValidMeteringPointTypes => new()
    {
        MeteringPointType.Production,
        MeteringPointType.Consumption,
    };

    public static TheoryData<MeteringPointType> InvalidMeteringPointTypes =>
    [
        ..EnumerationRecordType.GetAll<MeteringPointType>()
            .Except(ValidMeteringPointTypes),
    ];

    [Fact]
    public async Task Given_NoMasterData_When_Validate_Then_NoValidationError()
    {
        var input = new RequestYearlyMeasurementsInputV1Builder()
            .Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                null));

        result.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ValidMeteringPointTypes))]
    public async Task Given_ValidMeteringPointType_When_Validate_Then_NoValidationError(MeteringPointType meteringPointType)
    {
        var input = new RequestYearlyMeasurementsInputV1Builder()
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                meteringPointType: meteringPointType);

        var result = await _sut.ValidateAsync(
            new(
                input,
                meteringPointMasterData));

        result.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidMeteringPointTypes))]
    public async Task Given_InvalidMeteringPointType_When_Validate_Then_NoValidationError(MeteringPointType invalidMeteringPointType)
    {
        var input = new RequestYearlyMeasurementsInputV1Builder()
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                meteringPointType: invalidMeteringPointType);

        var result = await _sut.ValidateAsync(
            new(
                input,
                meteringPointMasterData));

        var validationError = Assert.Single(result);
        Assert.Equal(MeteringPointTypeValidationRule.WrongMeteringPointError.First(), validationError);
    }
}
