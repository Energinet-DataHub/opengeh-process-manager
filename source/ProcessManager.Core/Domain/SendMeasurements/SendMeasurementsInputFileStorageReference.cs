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

public record SendMeasurementsInputFileStorageReference : FileStorageReference
{
    /// <summary>
    /// The container must also be created in the infrastructure (st-filestorage.tf)
    /// </summary>
    public const string ContainerName = "send-measurements-instance-input";

    private SendMeasurementsInputFileStorageReference(
        string path)
    {
        Path = path;
    }

    public override string Path { get; }

    public override string Category => ContainerName;

    public static SendMeasurementsInputFileStorageReference Create(Instant createdAt, ActorNumber createdBy, TransactionId transactionId)
    {
        // "-" is not allowed in Azure Blob Storage paths, so we remove it from the transaction ID.
        var sanitizedTransactionId = transactionId.Value.Replace("-", string.Empty);

        var dateTimeUtc = createdAt.ToDateTimeUtc();
        var path = $"{createdBy.Value}/{dateTimeUtc.Year:0000}/{dateTimeUtc.Month:00}/{dateTimeUtc.Day:00}/{sanitizedTransactionId}";

        return new SendMeasurementsInputFileStorageReference(path);
    }
}
