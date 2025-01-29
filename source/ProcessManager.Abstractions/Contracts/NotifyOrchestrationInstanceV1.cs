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

public partial class NotifyOrchestrationInstanceV1
{
    public const string MajorVersion = nameof(NotifyOrchestrationInstanceV1);

    public void SetData<TNotifyData>(TNotifyData data)
        where TNotifyData : class?, INotifyDataDto
    {
        ArgumentNullException.ThrowIfNull(data);

        Data = new NotifyOrchestrationInstanceDataV1
        {
            Data = JsonSerializer.Serialize(data),
            DataFormat = NotifyOrchestrationInstanceDataFormatV1.Json,
            DataType = typeof(TNotifyData).Name,
        };
    }

    public TNotifyData? ParseData<TNotifyData>()
        where TNotifyData : class?
    {
        if (Data is null)
            return null;

        if (Data.DataType != typeof(TNotifyData).Name)
        {
            throw new InvalidOperationException($"Incorrect data type in received NotifyOrchestrationInstanceV1 message (TargetType={typeof(TNotifyData).Name}, DataType={Data.DataType})")
            {
                Data =
                {
                    { "TargetType", typeof(TNotifyData).Name },
                    { nameof(OrchestrationInstanceId), OrchestrationInstanceId },
                    { nameof(EventName), EventName },
                    { nameof(MajorVersion), MajorVersion },
                    { nameof(Data.DataFormat), Data.DataFormat },
                    { nameof(Data.DataType), Data.DataType },
                    {
                        nameof(Data.Data), Data.Data.Length < 1000
                            ? Data.Data
                            : Data.Data.Substring(0, 1000)
                    },
                },
            };
        }

        var result = Data.DataFormat switch
        {
            NotifyOrchestrationInstanceDataFormatV1.Json => JsonSerializer.Deserialize<TNotifyData>(Data.Data),
            _ => throw new ArgumentOutOfRangeException(
                nameof(Data.DataFormat),
                Data.DataFormat,
                $"Unhandled data format in received {nameof(NotifyOrchestrationInstanceV1)} message"),
        };

        if (result is null)
        {
            throw new InvalidOperationException($"Unable to deserialize {nameof(NotifyOrchestrationInstanceV1)} data")
            {
                Data =
                {
                    { nameof(OrchestrationInstanceId), OrchestrationInstanceId },
                    { nameof(EventName), EventName },
                    { nameof(MajorVersion), MajorVersion },
                    { nameof(Data.DataFormat), Data.DataFormat },
                    { nameof(Data.DataType), Data.DataType },
                    {
                        nameof(Data.Data), Data.Data.Length < 1000
                            ? Data.Data
                            : Data.Data.Substring(0, 1000)
                    },
                },
            };
        }

        return result;
    }
}
