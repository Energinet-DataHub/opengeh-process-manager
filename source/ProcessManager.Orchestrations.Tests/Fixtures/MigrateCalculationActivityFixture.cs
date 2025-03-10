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

using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

public class MigrateCalculationActivityFixture : IAsyncLifetime
{
    public MigrateCalculationActivityFixture()
    {
        WholesaleDatabaseManager = new WholesaleDatabaseManager("Wholesale");
        PMDatabaseManager = new ProcessManagerDatabaseManager("ProcessManager");
    }

    public WholesaleDatabaseManager WholesaleDatabaseManager { get; }

    public ProcessManagerDatabaseManager PMDatabaseManager { get; }

    public async Task InitializeAsync()
    {
        await WholesaleDatabaseManager.CreateDatabaseAsync();
        await PMDatabaseManager.CreateDatabaseAsync();

        // Describe BRS 023 / 027
        using var processManagerContext = PMDatabaseManager.CreateDbContext();
        var builder = new Orchestrations.Processes.BRS_023_027.V1.OrchestrationDescriptionBuilder();
        await processManagerContext.OrchestrationDescriptions.AddAsync(builder.Build());
        await processManagerContext.CommitAsync();
    }

    public async Task DisposeAsync()
    {
        await WholesaleDatabaseManager.DeleteDatabaseAsync();
        await PMDatabaseManager.DeleteDatabaseAsync();
    }
}
