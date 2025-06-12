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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.BusinessValidation;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_024.V1.BusinessValidation;

public sealed class ActorRoleValidationRuleTests
{
    private readonly ActorRoleValidationRule _sut = new();

    public static TheoryData<ActorRole> ValidActorRoles => new()
    {
        ActorRole.EnergySupplier,
    };

    public static TheoryData<ActorRole> InvalidActorRoles =>
    [
        ..EnumerationRecordType.GetAll<ActorRole>()
            .Except(ValidActorRoles),
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
    [MemberData(nameof(ValidActorRoles))]
    public async Task ValidateAsync_WhenRequestingWithValidActorRole_ReturnsEmptyErrorListAsync(ActorRole actorRole)
    {
        var input = new RequestYearlyMeasurementsInputV1Builder()
            .WithActorRole(actorRole)
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(input);

        var result = await _sut.ValidateAsync(
            new(
                input,
                meteringPointMasterData));

        result.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidActorRoles))]
    public async Task ValidateAsync_WhenRequestingWithUnexpectedActorRole_ReturnsEmptyErrorListAsync(ActorRole actorRole)
    {
        var input = new RequestYearlyMeasurementsInputV1Builder()
            .WithActorRole(actorRole)
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(input);

        var result = await _sut.ValidateAsync(
            new(
                input,
                meteringPointMasterData));

        var validationError = Assert.Single(result);
        Assert.Equal(ActorRoleValidationRule.WrongActorRoleError.First(), validationError);
    }
}
