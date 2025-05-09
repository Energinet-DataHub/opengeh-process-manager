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

namespace Energinet.DataHub.ProcessManager.Abstractions.Contracts;

/// <summary>
/// Framework container used for starting orchestration instances via service bus messages.
/// </summary>
/// <remarks>
/// This class defines the contract and payload format for initiating a versioned orchestration.
/// It provides methods for serializing input data into a format suitable for transport and
/// for safely deserializing the input on the receiving end.
///
/// All messages with <see cref="MajorVersion"/> matching this type should be parsed as
/// <c>StartOrchestrationInstanceV1</c>. This class is central to framework-level orchestration dispatching.
/// </remarks>
public partial class StartOrchestrationInstanceV1
{
    /// <summary>
    /// Constant indicating the major version of this message contract.
    /// </summary>
    public const string MajorVersion = nameof(StartOrchestrationInstanceV1);

    /// <summary>
    /// Sets the input data payload for the orchestration.
    /// The input is serialized to JSON, and metadata about its type and format are stored.
    /// </summary>
    /// <typeparam name="TInputData">The type of the input data.</typeparam>
    /// <param name="data">The input data to be serialized and stored.</param>
    public void SetInput<TInputData>(TInputData data)
        where TInputData : class, IInputParameterDto
    {
        Input = JsonSerializer.Serialize(data);
        InputFormat = StartOrchestrationInstanceInputFormatV1.Json;
        InputType = typeof(TInputData).Name;
    }

    /// <summary>
    /// Parses and deserializes the input data into the specified target type.
    /// </summary>
    /// <typeparam name="TInputData">The expected input type.</typeparam>
    /// <returns>The deserialized input data.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the input type does not match the expected type or deserialization fails.
    /// </exception>
    public TInputData ParseInput<TInputData>()
        where TInputData : class?
    {
        if (InputType != typeof(TInputData).Name)
        {
            throw new InvalidOperationException($"Incorrect data type in received StartOrchestrationInstanceV1 message (TargetType={typeof(TInputData).Name}, InputType={InputType})")
            {
                Data =
                {
                    { "TargetType", typeof(TInputData).Name },
                    { nameof(OrchestrationName), OrchestrationName },
                    { nameof(OrchestrationVersion), OrchestrationVersion },
                    { nameof(MajorVersion), MajorVersion },
                    { nameof(InputFormat), InputFormat },
                    { nameof(InputType), InputType },
                    {
                        nameof(Input), Input.Length < 1000
                            ? Input
                            : Input.Substring(0, 1000)
                    },
                },
            };
        }

        var result = InputFormat switch
        {
            StartOrchestrationInstanceInputFormatV1.Json => JsonSerializer.Deserialize<TInputData>(Input),
            _ => throw new ArgumentOutOfRangeException(
                nameof(InputFormat),
                InputFormat,
                $"Unhandled input format in received {nameof(StartOrchestrationInstanceV1)} message"),
        };

        if (result is null)
        {
            throw new InvalidOperationException($"Unable to deserialize {nameof(StartOrchestrationInstanceV1)} data")
            {
                Data =
                {
                    { "TargetType", typeof(TInputData).Name },
                    { nameof(OrchestrationName), OrchestrationName },
                    { nameof(OrchestrationVersion), OrchestrationVersion },
                    { nameof(MajorVersion), MajorVersion },
                    { nameof(InputFormat), InputFormat },
                    { nameof(InputType), InputType },
                    {
                        nameof(Input), Input.Length < 1000
                            ? Input
                            : Input.Substring(0, 1000)
                    },
                },
            };
        }

        return result;
    }
}
