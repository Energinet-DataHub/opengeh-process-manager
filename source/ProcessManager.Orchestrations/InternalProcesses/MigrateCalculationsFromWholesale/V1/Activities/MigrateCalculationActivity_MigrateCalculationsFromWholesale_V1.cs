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
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;

public class MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1
{
    public const string MigratedWholesaleCalculationIdCustomStatePrefix = "MigratedWholesaleCalculationId=";

    public static OrchestrationInstanceCustomState GetMigratedWholesaleCalculationIdCustomState(Guid wholesaleCalculationId)
    {
        return new OrchestrationInstanceCustomState(
            $"{MigratedWholesaleCalculationIdCustomStatePrefix}{wholesaleCalculationId}");
    }

    public static Guid GetMigratedWholesaleCalculationIdCustomStateGuid(OrchestrationInstanceCustomState customState)
    {
        var calculationIdString = customState.Value.Replace(MigratedWholesaleCalculationIdCustomStatePrefix, string.Empty);
        var calculationId = Guid.Parse(calculationIdString);
        return calculationId;
    }

    [Function(nameof(MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1))]
    public async Task<string> Run(
        [ActivityTrigger] ActivityInput input)
    {
        // TODO: Implement migration
        await Task.CompletedTask.ConfigureAwait(false);

        return $"Migrated {input.CalculationToMigrateId}";
    }

    public record ActivityInput(
        Guid CalculationToMigrateId);
}
