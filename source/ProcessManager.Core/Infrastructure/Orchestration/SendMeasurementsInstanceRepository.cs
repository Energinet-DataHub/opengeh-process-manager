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

using Energinet.DataHub.ProcessManager.Core.Application.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;

public class SendMeasurementsInstanceRepository(
    ProcessManagerContext dbContext,
    IFileStorageClient fileStorageClient)
        : ISendMeasurementsInstanceRepository
{
    private readonly ProcessManagerContext _dbContext = dbContext;
    private readonly IFileStorageClient _fileStorageClient = fileStorageClient;

    public IUnitOfWork UnitOfWork => _dbContext;

    public Task AddAsync(SendMeasurementsInstance instance, Stream input)
    {
        _dbContext.SendMeasurementsInstances.Add(instance);

        var uploadTask = _fileStorageClient.UploadAsync(instance.FileStorageReference, input);

        return uploadTask;
    }

    public async Task<SendMeasurementsInstance> GetAsync(SendMeasurementsInstanceId id)
    {
        var instance = await _dbContext.SendMeasurementsInstances
            .FindAsync(id)
            .ConfigureAwait(false);

        return instance ?? throw new NullReferenceException($"{nameof(SendMeasurementsInstance)} not found (Id={id.Value}).");
    }

    public Task<SendMeasurementsInstance?> GetOrDefaultAsync(IdempotencyKey idempotencyKey)
    {
        return _dbContext.SendMeasurementsInstances
            .SingleOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey.ToHash());
    }
}
