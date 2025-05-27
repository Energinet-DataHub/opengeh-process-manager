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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Unit.Infrastructure.Extensions;

public class ActorRoleExtensionsTest
{
    private static readonly IReadOnlyDictionary<ActorRole, byte> _expectedActorRoleToByteValueMap = new Dictionary<ActorRole, byte>
    {
        { ActorRole.MeteringPointAdministrator, 1 },
        { ActorRole.EnergySupplier, 2 },
        { ActorRole.GridAccessProvider, 3 },
        { ActorRole.MeteredDataAdministrator, 4 },
        { ActorRole.MeteredDataResponsible, 5 },
        { ActorRole.BalanceResponsibleParty, 6 },
        { ActorRole.ImbalanceSettlementResponsible, 7 },
        { ActorRole.SystemOperator, 8 },
        { ActorRole.DanishEnergyAgency, 9 },
        { ActorRole.Delegated, 10 },
        { ActorRole.DataHubAdministrator, 11 },
    };

    public static TheoryData<ActorRole> AllActorRoleValues()
    {
        return new TheoryData<ActorRole>(EnumerationRecordType.GetAll<ActorRole>());
    }

    public static TheoryData<byte> AllActorRoleByteValues()
    {
        return new TheoryData<byte>(_expectedActorRoleToByteValueMap.Values);
    }

    /// <summary>
    /// Ensure that the expected mappings dictionary contains all actor roles defined in the actual mapper.
    /// </summary>
    [Fact]
    public void Given_ExpectedMappingsDictionary_When_ComparedToActualMappingsDictionary_Then_TheyContainTheSameActorRoles()
    {
        var expectedActorRoles = _expectedActorRoleToByteValueMap.Keys.ToList();

        var actualMappedActorRoles = ActorRoleExtensions.ActorRoleToByteValueMap.Keys.ToList();

        Assert.Equal(expectedActorRoles, actualMappedActorRoles);
    }

    [Fact]
    public void Given_MappingsDictionary_When_Created_Then_ActorRolesAreDistinct()
    {
        var mappedActorRoles = ActorRoleExtensions.ActorRoleToByteValueMap.Keys.ToList();

        Assert.Distinct(mappedActorRoles);
    }

    [Fact]
    public void Given_MappingsDictionary_When_Created_Then_ByteValuesAreDistinct()
    {
        var mappedByteValues = ActorRoleExtensions.ActorRoleToByteValueMap.Values.ToList();

        Assert.Distinct(mappedByteValues);
    }

    /// <summary>
    /// Ensure all actor roles can be mapped to their expected byte values.
    /// </summary>
    /// <param name="actorRole"></param>
    [Theory]
    [MemberData(nameof(AllActorRoleValues))]
    public void Given_ActorRole_When_ToByteValue_Then_MapsToExpectedByteValue(ActorRole actorRole)
    {
        var byteValue = actorRole.ToByteValue();
        Assert.Equal(expected: _expectedActorRoleToByteValueMap[actorRole], actual: byteValue);
    }

    /// <summary>
    /// Ensure all byte values can be mapped to their expected actor roles.
    /// </summary>
    /// <param name="byteValue"></param>
    [Theory]
    [MemberData(nameof(AllActorRoleByteValues))]
    public void Given_ByteValue_When_ToActorRole_Then_MapsToExpectedActorRole(byte byteValue)
    {
        var expectedActorRole = _expectedActorRoleToByteValueMap.Single(x => x.Value == byteValue).Key;

        var actorRole = byteValue.ToActorRole();
        Assert.Equal(expected: expectedActorRole, actual: actorRole);
    }

    [Fact]
    public void Given_UnknownActorRole_When_ToByteValue_Then_Throws()
    {
        // Create Actor Role with invalid "EnergySupplier2" name
        var invalidActorRole = (ActorRole?)Activator.CreateInstance(
            type: typeof(ActorRole),
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: ["UnknownRoleName"],
            culture: null)!;

        Assert.Throws<KeyNotFoundException>(() => invalidActorRole.ToByteValue());
    }

    [Fact]
    public void Given_UnknownByteValue_When_ToActorRole_Then_Throws()
    {
        // Create Actor Role with invalid "EnergySupplier2" name
        const byte invalidByteValue = (byte)99; // Assuming 99 is not a valid byte value for any ActorRole

        Assert.Throws<KeyNotFoundException>(() => invalidByteValue.ToActorRole());
    }
}
