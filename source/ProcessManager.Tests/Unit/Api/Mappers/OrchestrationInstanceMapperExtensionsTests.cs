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

using System.Text.Json;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;
using FluentAssertions.Execution;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Tests.Unit.Api.Mappers;

public class OrchestrationInstanceMapperExtensionsTests
{
    public static IEnumerable<object[]> GetOperatingIdentity()
    {
        return new List<object[]>
        {
            new object[] { DomainTestDataFactory.BalanceResponsibleParty.ActorIdentity },
            new object[] { DomainTestDataFactory.BalanceResponsibleParty.UserIdentity },
        };
    }

    [Fact]
    public void MapToTypedDto_WhenOrchestrationInstance_CreateOrchestrationInstanceTypedDtoThatCanBeFullySerializedToJson()
    {
        var orchestrationInstance = CreateOrchestrationInstance(
            idempotencyKey: new IdempotencyKey(Guid.NewGuid().ToString()));

        // Act
        var actualDto = orchestrationInstance.MapToTypedDto<TestOrchestrationParameter>();
        var dtoAsJson = JsonSerializer.Serialize(actualDto);

        // Assert
        using var assertionScope = new AssertionScope();
        var typedDto = JsonSerializer.Deserialize<OrchestrationInstanceTypedDto<TestOrchestrationParameter>>(dtoAsJson);
        typedDto!.IdempotencyKey.Should().Be(orchestrationInstance.IdempotencyKey!.Value);
        typedDto!.ParameterValue.TestString.Should().NotBeNull();
        typedDto!.ParameterValue.TestInt.Should().NotBeNull();
    }

    /// <summary>
    /// Even the 'ParameterValue' is mapped in a way that allows us to serialize the object
    /// and deserialize it to a strongly typed orchestration instance with parmeters.
    /// </summary>
    [Fact]
    public void MapToDto_WhenOrchestrationInstance_CreateOrchestrationInstanceDtoThatCanBeFullySerializedToJson()
    {
        var orchestrationInstance = CreateOrchestrationInstance(
            idempotencyKey: new IdempotencyKey(Guid.NewGuid().ToString()));

        // Act
        // => We create and serialize 'OrchestrationInstanceDto'
        var actualDto = orchestrationInstance.MapToDto();
        var dtoAsJson = JsonSerializer.Serialize(actualDto);

        // Assert
        // => But we can deserialize to specific 'OrchestrationInstanceTypedDto<TestOrchestrationParameter>'
        var typedDto = JsonSerializer.Deserialize<OrchestrationInstanceTypedDto<TestOrchestrationParameter>>(dtoAsJson);
        typedDto!.IdempotencyKey.Should().Be(orchestrationInstance.IdempotencyKey!.Value);
        typedDto!.ParameterValue.TestString.Should().NotBeNull();
        typedDto!.ParameterValue.TestInt.Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(GetOperatingIdentity))]
    public void MapToDto_WhenOrchestrationInstanceWithOperatingIdentity_DeserializedToExpectedType(OperatingIdentity operatingIdentity, Type expectedType)
    {
        var orchestrationInstance = CreateOrchestrationInstance(operatingIdentity);

        // Act
        var actualDto = orchestrationInstance.MapToDto();
        var dtoAsJson = JsonSerializer.Serialize(actualDto);

        // Assert
        var dto = JsonSerializer.Deserialize<OrchestrationInstanceDto>(dtoAsJson);
        dto!.Lifecycle.CreatedBy.Should().BeOfType(expectedType);
    }

    private static OrchestrationInstance CreateOrchestrationInstance(
        OperatingIdentity? createdBy = default,
        IdempotencyKey? idempotencyKey = default)
    {
        var orchestrationDescription = DomainTestDataFactory.CreateOrchestrationDescription();

        var operatingIdentity = createdBy
            ?? DomainTestDataFactory.EnergySupplier.UserIdentity;

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            operatingIdentity,
            orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            runAt: null,
            idempotencyKey: idempotencyKey,
            actorMessageId: new ActorMessageId(Guid.NewGuid().ToString()),
            transactionId: new TransactionId(Guid.NewGuid().ToString()),
            meteringPointId: new MeteringPointId(Guid.NewGuid().ToString()));

        orchestrationInstance.ParameterValue.SetFromInstance(new TestOrchestrationParameter
        {
            TestString = "Test string",
            TestInt = 42,
        });

        return orchestrationInstance;
    }

    private class TestOrchestrationParameter : IInputParameterDto
    {
        public string? TestString { get; set; }

        public int? TestInt { get; set; }
    }
}
