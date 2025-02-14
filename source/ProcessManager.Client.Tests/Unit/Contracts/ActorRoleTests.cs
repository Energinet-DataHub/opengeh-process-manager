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
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Unit.Contracts;

public class ActorRoleTests
{
    public static TheoryData<ActorRoleV1> AllActorRoleV1Values()
    {
        return new TheoryData<ActorRoleV1>(
            values: Enum.GetValues<ActorRoleV1>()
                .Where(actorRoleV1 => actorRoleV1 != ActorRoleV1.UnspecifiedRole));
    }

    public static TheoryData<ActorRole> AllActorRoleValues()
    {
        return new TheoryData<ActorRole>(
            values: EnumerationRecordType.GetAll<ActorRole>());
    }

    /// <summary>
    /// Ensure that all ActorRoleV1 values can be mapped to ActorRole
    /// </summary>
    [Theory]
    [MemberData(nameof(AllActorRoleV1Values))]
    public void Given_ActorRoleV1_When_MappingToActorRole_Then_Success(ActorRoleV1 actorRoleV1)
    {
        var act = () => ActorRole.From(actorRoleV1);
        act.Should().NotThrow();
    }

    /// <summary>
    /// Ensure that all ActorRole values can be mapped to ActorRoleV1.
    /// Maybe this isn't necessary and we only need to ensure all ActorRoleV1 values can be mapped to an ActorRole.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllActorRoleValues))]
    public void Given_ActorRole_When_MappingToActorRoleV1_Then_Success(ActorRole actorRole)
    {
        var act = actorRole.ToActorRoleV1;
        act.Should().NotThrow();
    }

    [Fact]
    public void Given_InvalidActorRoleV1_When_MappingToActorRole_Then_Throws()
    {
        // Create ActorRoleV1 with invalid "-161" value
        const ActorRoleV1 invalidActorRoleV1 = (ActorRoleV1)(-161);

        var act = () => ActorRole.From(invalidActorRoleV1);
        act.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void Given_InvalidActorRole_When_MappingToActorRoleV1_Then_Throws()
    {
        // Create Actor Role with invalid "EnergySupplier2" name
        var invalidActorRole = (ActorRole?)Activator.CreateInstance(
            type: typeof(ActorRole),
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: ["EnergySupplier2"],
            culture: null)!;

        var act = () => invalidActorRole.ToActorRoleV1();
        act.Should().ThrowExactly<ArgumentOutOfRangeException>();
    }
}
