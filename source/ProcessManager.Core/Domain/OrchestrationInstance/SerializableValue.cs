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
using System.Text.Json;

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

/// <summary>
/// Support storing a string value as JSON.
/// </summary>
public class SerializableValue
{
    internal SerializableValue()
    {
        SerializedValue = string.Empty;
    }

    public bool IsEmpty => SerializedValue == string.Empty;

    /// <summary>
    /// The JSON representation of the value.
    /// </summary>
    public string SerializedValue { get; private set; }

    /// <summary>
    /// Serialize the value from an instance.
    /// An value must be a <see langword="class"/>
    /// (which includes <see langword="record"/>), and be serializable to JSON.
    /// </summary>
    public void SetFromInstance<TValue>(TValue instance)
        where TValue : class
    {
        SerializedValue = JsonSerializer.Serialize(instance);
    }

    public ExpandoObject AsExpandoObject()
    {
        if (IsEmpty)
            return new ExpandoObject();

        return JsonSerializer.Deserialize<ExpandoObject>(SerializedValue)
            ?? new ExpandoObject();
    }

    public TValue AsType<TValue>()
        where TValue : class
    {
        return JsonSerializer.Deserialize<TValue>(SerializedValue)
            ?? throw new InvalidOperationException($"Could not deserialize as type '{typeof(TValue)}'.");
    }
}
