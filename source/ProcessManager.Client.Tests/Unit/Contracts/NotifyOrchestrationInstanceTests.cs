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

using DurableTask.Core.Serializing;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using FluentAssertions;
using Newtonsoft.Json;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Unit.Contracts;

public class NotifyOrchestrationInstanceTests
{
    [Fact]
    public void Given_ObjectContainsData_When_DeserializingToAbstractType_Then_DurableFunctionSerializerCanHandleData()
    {
        // Given object with data
        var notifyOrchestrationInstance = new NotifyOrchestrationInstanceV1
        {
            OrchestrationInstanceId = "test-orchestration-instance-id",
            EventName = "test-event",
        };

        var expectedData = new TestData("Test message");
        notifyOrchestrationInstance.SetData(expectedData);

        // When deserializing to abstract type
        // => We must use Newtonsoft.Json JsonConverter to deserialize to abstract type, else the Durable Function
        // JSON converter doesn't work :(.
        var genericData = JsonConvert.DeserializeObject(notifyOrchestrationInstance.Data.Data);
        ArgumentNullException.ThrowIfNull(genericData);

        // Then Durable Function serializer can serialize and deserialize abstract data correctly
        var durableFunctionJsonConverter = new JsonDataConverter();

        // => Serialize generic data to JSON string using Durable Function serializer
        var serializedGenericData = durableFunctionJsonConverter.Serialize(genericData);

        // => When the serialized data is deserialized it is equal to the expected data
        var deserializedTestData = durableFunctionJsonConverter.Deserialize<TestData>(serializedGenericData);
        deserializedTestData.Should().BeEquivalentTo(expectedData);
    }

    private record TestData(string Message) : INotifyDataDto;
}
