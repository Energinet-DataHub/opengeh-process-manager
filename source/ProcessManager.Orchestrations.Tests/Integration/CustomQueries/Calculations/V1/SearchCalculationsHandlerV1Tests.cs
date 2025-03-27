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

using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.CustomQueries.Calculations.V1;

public class SearchCalculationsHandlerV1Tests :
    IClassFixture<ProcessManagerDatabaseFixture>,
    IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly ProcessManagerReaderContext _readerContext;
    private readonly SearchCalculationsHandlerV1 _sut;

    public SearchCalculationsHandlerV1Tests(ProcessManagerDatabaseFixture fixture)
    {
        _fixture = fixture;
        _readerContext = fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();
        _sut = new SearchCalculationsHandlerV1(_readerContext);
    }

    public async Task InitializeAsync()
    {
        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        await dbContext.OrchestrationInstances.ExecuteDeleteAsync();
        await dbContext.OrchestrationDescriptions.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await _readerContext.DisposeAsync();
    }
}
