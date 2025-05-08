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

namespace Energinet.DataHub.ProcessManager.Abstractions.Contracts;

public partial class StartOrchestrationInstanceV1 //TODO: LRN document clearly that this is the framework container for startring orchestration instances
{
    public const string MajorVersion = nameof(StartOrchestrationInstanceV1);

    public void SetInput<TInputData>(TInputData data)
        where TInputData : class
    {
        Input = JsonSerializer.Serialize(data);
        InputFormat = StartOrchestrationInstanceInputFormatV1.Json;
        InputType = typeof(TInputData).Name;
    }

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
