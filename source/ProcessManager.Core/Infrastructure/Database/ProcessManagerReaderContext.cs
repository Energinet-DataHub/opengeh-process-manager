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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;

/// <summary>
/// A database context that should only be used from custom query handlers.
/// </summary>
/// <param name="options"></param>
public class ProcessManagerReaderContext(
    DbContextOptions<ProcessManagerReaderContext> options) :
        DbContext(options)
{
    public DbSet<OrchestrationDescription> OrchestrationDescriptions { get; private set; }

    public DbSet<OrchestrationInstance> OrchestrationInstances { get; private set; }

    #region Save Changes (not supported)

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("The reader context doesn't support saving changes.");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("The reader context doesn't support saving changes.");
    }

    public override int SaveChanges()
    {
        throw new NotSupportedException("The reader context doesn't support saving changes.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new NotSupportedException("The reader context doesn't support saving changes.");
    }

    #endregion

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pm");
        modelBuilder.ApplyConfiguration(new OrchestrationDescriptionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrchestrationInstanceEntityConfiguration());
    }
}
