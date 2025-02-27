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
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Models;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;

internal class GetCalculationsToMigrateActivity_MigrateCalculationsFromWholesale_V1(
    WholesaleContext wholesaleContext,
    ProcessManagerContext processManagerContext)
{
    private readonly WholesaleContext _wholesaleContext = wholesaleContext;
    private readonly ProcessManagerContext _processManagerContext = processManagerContext;

    [Function(nameof(GetCalculationsToMigrateActivity_MigrateCalculationsFromWholesale_V1))]
    public async Task<CalculationsToMigrate> Run(
        [ActivityTrigger] FunctionContext functionContext)
    {
        var allWholesaleCalculationsIds = await _wholesaleContext
            .CreateCalculationsToMigrateQuery()
            .Select(c => c.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var alreadyMigratedCalculations = await _processManagerContext
            .CreateMigratedCalculationsQuery()
            .ToListAsync()
            .ConfigureAwait(false);

        var alreadyMigratedCalculationIds = alreadyMigratedCalculations
            .Select(oi => oi.CustomState.AsType<MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.CustomState>())
            .Select(cs => cs.MigratedWholesaleCalculationId)
            .ToList();

        var remainingCalculationsIdsToMigrate = allWholesaleCalculationsIds
            .Where(calculationId => !alreadyMigratedCalculationIds.Contains(calculationId))
            .ToList();

        var calculationsToMigrate = new CalculationsToMigrate(
            CalculationsToMigrateCount: remainingCalculationsIdsToMigrate.Count,
            CalculationIdsToMigrate: remainingCalculationsIdsToMigrate,
            AllWholesaleCalculationsCount: allWholesaleCalculationsIds.Count,
            AllWholesaleCalculationIds: allWholesaleCalculationsIds,
            AlreadyMigratedCalculationsCount: alreadyMigratedCalculationIds.Count,
            AlreadyMigratedCalculationIds: alreadyMigratedCalculationIds);

        return calculationsToMigrate;
    }
}
