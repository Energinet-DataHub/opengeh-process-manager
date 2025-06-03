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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;

public record SendMeasurementsInputFileStorageReference : IFileStorageReference
{
    /// <summary>
    /// The container must already be created in Azure Blob Storage (see st-filestorage.tf).
    /// </summary>
    public const string ContainerName = "send-measurements-input";

    private SendMeasurementsInputFileStorageReference(
        string path)
    {
        Path = path;
    }

    public string Path { get; }

    /// <summary>
    /// The category is used as the container name in Azure Blob Storage.
    /// </summary>
    public string Category => ContainerName;

    public static SendMeasurementsInputFileStorageReference Create(Instant createdAt, ActorNumber createdBy, SendMeasurementsInstanceId id)
    {
        // "-" is not allowed in Azure Blob Storage paths, so we remove it from the instance id.
        var sanitizedId = id.Value.ToString("N");

        var dateTimeUtc = createdAt.ToDateTimeUtc();
        var path = $"{createdBy.Value}/{dateTimeUtc.Year:0000}/{dateTimeUtc.Month:00}/{dateTimeUtc.Day:00}/{sanitizedId}";

        return new SendMeasurementsInputFileStorageReference(path);
    }
}
