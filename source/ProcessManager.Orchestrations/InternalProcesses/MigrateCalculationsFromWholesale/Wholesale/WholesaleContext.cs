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

using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale.Model;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;

public class WholesaleContext : DbContext
{
    private const string Schema = "calculations";

    public WholesaleContext(DbContextOptions<WholesaleContext> options)
        : base(options)
    {
    }

    // Added to support Moq in tests
    public WholesaleContext()
    {
    }

    public virtual DbSet<Calculation> Calculations { get; private set; } = null!;

    public IQueryable<Calculation> CreateCalculationsToMigrateQuery()
    {
        return Calculations
            .AsNoTracking()
            .Where(c => c.OrchestrationState == CalculationOrchestrationState.Completed);
    }

    public Task<int> SaveChangesAsync() => base.SaveChangesAsync();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new CalculationEntityConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
