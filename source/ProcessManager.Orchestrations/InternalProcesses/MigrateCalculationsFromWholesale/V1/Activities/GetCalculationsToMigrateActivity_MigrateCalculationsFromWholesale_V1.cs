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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.InternalProcesses.MigrateCalculationsFromWholesale;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Models;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale.Model;
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
        var allWholesaleCalculationsIds = await _wholesaleContext.Calculations
            .Where(c => c.OrchestrationState == CalculationOrchestrationState.Completed)
            .AsNoTracking()
            .Select(c => c.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var alreadyMigratedCalculations = await _processManagerContext.OrchestrationDescriptions
            .Where(od => od.UniqueName.Name == MigrateCalculationsFromWholesaleUniqueName.V1.Name
                         && od.UniqueName.Version == MigrateCalculationsFromWholesaleUniqueName.V1.Version)
            .Join(
                inner: _processManagerContext.OrchestrationInstances,
                outerKeySelector: od => od.Id,
                innerKeySelector: oi => oi.OrchestrationDescriptionId,
                resultSelector: (od, oi) => oi)
            .Where(oi => oi.CustomState.Value.Contains(MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.MigratedWholesaleCalculationIdCustomStatePrefix))
            .AsNoTracking()
            .ToListAsync()
            .ConfigureAwait(false);

        var alreadyMigratedCalculationIds = alreadyMigratedCalculations
            .Select(oi => oi.CustomState)
            .Select(MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.GetMigratedWholesaleCalculationIdCustomStateGuid)
            .ToList();

        var remainingCalculationsIdsToMigrate = allWholesaleCalculationsIds
            .Where(calculationId => !alreadyMigratedCalculationIds.Contains(calculationId))
            .ToList();

        var calculationsToMigrate = new CalculationsToMigrate(
            CalculationsToMigrateCount: remainingCalculationsIdsToMigrate.Count,
            CalculationIdsToMigrate: remainingCalculationsIdsToMigrate,
            AllWholesaleCalculationIds: allWholesaleCalculationsIds,
            AllWholesaleCalculationsCount: allWholesaleCalculationsIds.Count,
            AlreadyMigratedCalculationIds: alreadyMigratedCalculationIds,
            AlreadyMigratedCalculationsCount: alreadyMigratedCalculationIds.Count);

        return calculationsToMigrate;
    }
}
