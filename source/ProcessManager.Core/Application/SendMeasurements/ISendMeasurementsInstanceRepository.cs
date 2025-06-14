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

using Energinet.DataHub.ProcessManager.Core.Application.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;

namespace Energinet.DataHub.ProcessManager.Core.Application.SendMeasurements;

public interface ISendMeasurementsInstanceRepository
{
    /// <summary>
    /// Use <see cref="IUnitOfWork.CommitAsync"/> to save changes.
    /// </summary>
    IUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// Add the BRS-021 Send Measurements instance to the database, and upload the input to file storage.
    /// To commit changes use <see cref="UnitOfWork"/>.
    /// </summary>
    /// <param name="instance">The instance to add to the database.</param>
    /// <param name="input">The input (as a stream) to upload to file storage</param>
    Task AddAsync(SendMeasurementsInstance instance, Stream input);

    /// <summary>
    /// Get existing instance by id.
    /// </summary>
    Task<SendMeasurementsInstance> GetAsync(SendMeasurementsInstanceId id);

    /// <summary>
    /// Get existing instance by idempotency key.
    /// </summary>
    Task<SendMeasurementsInstance?> GetOrDefaultAsync(IdempotencyKey idempotencyKey);

    /// <summary>
    /// Download the input for an <see cref="SendMeasurementsInstance"/>, found by the given <paramref name="reference"/>.
    /// </summary>
    /// <remarks>The returned stream is only downloaded when it is read, and can only be read once.</remarks>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Returns a stream that is downloaded when it is read, and can only be read once.</returns>
    Task<ReadOnceStream> DownloadInputAsync(SendMeasurementsInputFileStorageReference reference, CancellationToken cancellationToken = default);
}
