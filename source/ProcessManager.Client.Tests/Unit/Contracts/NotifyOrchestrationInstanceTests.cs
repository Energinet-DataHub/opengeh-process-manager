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

using System.Dynamic;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit.Abstractions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Unit.Contracts;

public class NotifyOrchestrationInstanceTests
{
    private readonly ITestOutputHelper _output;

    public NotifyOrchestrationInstanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Given_ObjectContainsData_When_DeserializingToAbstractType_Then_DeserializedDataIsCorrect()
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
        var test = JsonSerializer.Deserialize<ExpandoObject>(notifyOrchestrationInstance.Data.Data);
        _output.WriteLine("Type of deserialized object: " + test?.GetType());
        _output.WriteLine("Deserialized object: " + test);

        var genericData = notifyOrchestrationInstance.ParseData<ExpandoObject>();
        ArgumentNullException.ThrowIfNull(genericData);

        // Then data is correct
        Assert.Equal(((dynamic)genericData).Message.ToString(), expectedData.Message);

        var serializedAgain = JsonSerializer.Serialize(genericData);
        serializedAgain.Should().Be(notifyOrchestrationInstance.Data.Data);
    }

    private record TestData(string Message);
}
