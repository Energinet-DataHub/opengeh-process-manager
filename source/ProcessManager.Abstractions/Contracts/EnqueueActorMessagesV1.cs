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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;

namespace Energinet.DataHub.ProcessManager.Abstractions.Contracts;

public partial class EnqueueActorMessagesV1
{
    public const string MajorVersion = nameof(EnqueueActorMessagesV1);

    /// <summary>
    /// Build the service bus message subject using the provided orchestration unique name.
    /// </summary>
    public static string BuildServiceBusMessageSubject(string orchestrationUniqueName) => $"Enqueue_{orchestrationUniqueName.ToLower()}";

    /// <summary>
    /// Build the service bus message subject using the provided orchestration unique name.
    /// </summary>
    public static string BuildServiceBusMessageSubject(OrchestrationDescriptionUniqueNameDto uniqueName) => BuildServiceBusMessageSubject(uniqueName.Name);

    public void SetData<TData>(TData data)
        where TData : class
    {
        Data = JsonSerializer.Serialize(data);
        DataFormat = EnqueueActorMessagesDataFormatV1.Json;
        DataType = typeof(TData).Name;
    }

    public TData ParseData<TData>()
        where TData : class
    {
        if (DataType != typeof(TData).Name)
        {
            throw new InvalidOperationException($"Incorrect data type in received EnqueueActorMessagesV1 message (TargetType={typeof(TData).Name}, DataType={DataType})")
            {
                Data =
                {
                    { "TargetType", typeof(TData).Name },
                    { nameof(OrchestrationInstanceId), OrchestrationInstanceId },
                    { nameof(MajorVersion), MajorVersion },
                    { nameof(DataFormat), DataFormat },
                    { nameof(DataType), DataType },
                    {
                        nameof(Data), Data.Length < 1000
                            ? Data
                            : Data.Substring(0, 1000)
                    },
                },
            };
        }

        var result = DataFormat switch
        {
            EnqueueActorMessagesDataFormatV1.Json => JsonSerializer.Deserialize<TData>(Data),
            _ => throw new ArgumentOutOfRangeException(
                nameof(DataFormat),
                DataFormat,
                $"Unhandled data format in received {nameof(EnqueueActorMessagesV1)} message"),
        };

        if (result is null)
        {
            throw new InvalidOperationException($"Unable to deserialize {nameof(EnqueueActorMessagesV1)} data")
            {
                Data =
                {
                    { "TargetType", typeof(TData).Name },
                    { nameof(OrchestrationInstanceId), OrchestrationInstanceId },
                    { nameof(MajorVersion), MajorVersion },
                    { nameof(DataFormat), DataFormat },
                    { nameof(DataType), DataType },
                    {
                        nameof(Data), Data.Length < 1000
                            ? Data
                            : Data.Substring(0, 1000)
                    },
                },
            };
        }

        return result;
    }
}
