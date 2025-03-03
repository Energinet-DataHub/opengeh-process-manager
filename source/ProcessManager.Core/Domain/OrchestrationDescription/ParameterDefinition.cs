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

using System.Text.Json;
using NJsonSchema;

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;

/// <summary>
/// Defines a Durable Functions orchestration input parameter type using a JSON schema.
/// </summary>
public class ParameterDefinition
{
    internal ParameterDefinition()
    {
        SerializedParameterDefinition = string.Empty;
    }

    /// <summary>
    /// The JSON schema defining the parameter type.
    /// </summary>
    internal string SerializedParameterDefinition { get; private set; }

    /// <summary>
    /// Set the parameter definition by specifying its type.
    /// An input parameter for Durable Functions orchestration must be a <see langword="class"/>
    /// (which includes <see langword="record"/>), and be serializable to JSON.
    /// </summary>
    public void SetFromType<TParameter>()
        where TParameter : class
    {
        var jsonSchema = JsonSchema.FromType<TParameter>();

        SerializedParameterDefinition = jsonSchema.ToJson();
    }

    /// <summary>
    /// Validate <paramref name="parameterValue"/> against the defining JSON schema.
    /// </summary>
    public Task<bool> IsValidParameterValueAsync(object parameterValue)
    {
        var serializedParameterValue = JsonSerializer.Serialize(parameterValue);

        return IsValidParameterValueAsync(serializedParameterValue);
    }

    /// <summary>
    /// Validate <paramref name="serializedParameterValue"/> against the defining JSON schema.
    /// </summary>
    public async Task<bool> IsValidParameterValueAsync(string serializedParameterValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedParameterValue);

        var jsonSchema = await JsonSchema.FromJsonAsync(SerializedParameterDefinition).ConfigureAwait(false);
        var errors = jsonSchema.Validate(serializedParameterValue);

        return errors.Count == 0;
    }

    internal void SetSerializedParameterDefinition(string serializedParameterDefinition)
    {
        SerializedParameterDefinition = serializedParameterDefinition;
    }
}
