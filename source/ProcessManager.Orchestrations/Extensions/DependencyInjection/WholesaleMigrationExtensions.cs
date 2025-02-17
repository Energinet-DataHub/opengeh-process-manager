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

using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.WholesaleMigration.Wholesale;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;

public static class WholesaleMigrationExtensions
{
    /// <summary>
    /// Register Process Manager database and health checks.
    /// Depends on <see cref="WholesaleMigrationOptions"/>.
    /// </summary>
    public static IServiceCollection AddWholesaleDatabase(this IServiceCollection services)
    {
        services
            .AddOptions<WholesaleMigrationOptions>()
            .BindConfiguration(WholesaleMigrationOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddDbContext<WholesaleContext>((sp, optionsBuilder) =>
            {
                var wholesaleOptions = sp.GetRequiredService<IOptions<WholesaleMigrationOptions>>().Value;

                optionsBuilder.UseSqlServer(wholesaleOptions.SqlDatabaseConnectionString, providerOptionsBuilder =>
                {
                    providerOptionsBuilder.UseNodaTime();
                    providerOptionsBuilder.EnableRetryOnFailure();
                });
            });

        services
            .AddHealthChecks()
            .AddDbContextCheck<WholesaleContext>(name: "WholesaleDatabase");

        return services;
    }
}
