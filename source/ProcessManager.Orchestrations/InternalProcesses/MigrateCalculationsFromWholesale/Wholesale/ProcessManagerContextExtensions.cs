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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;

public static class ProcessManagerContextExtensions
{
    public static IQueryable<OrchestrationInstance> CreateMigratedCalculationsQuery(
        this ProcessManagerContext context)
    {
        return context.OrchestrationDescriptions
            .AsNoTracking()
            .Where(
                od =>
                    od.UniqueName.Name == Brs_023_027.V1.Name
                    && od.UniqueName.Version == Brs_023_027.V1.Version)
            .Join(
                inner: context.OrchestrationInstances,
                outerKeySelector: od => od.Id,
                innerKeySelector: oi => oi.OrchestrationDescriptionId,
                resultSelector: (od, oi) => oi)
            .AsNoTracking()
            .Where(
                oi => oi.CustomState.Value.Contains(
                    MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1
                        .MigratedWholesaleCalculationIdCustomStatePrefix));
    }
}
