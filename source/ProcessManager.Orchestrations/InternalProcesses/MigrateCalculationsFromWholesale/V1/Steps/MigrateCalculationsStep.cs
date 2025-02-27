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
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Models;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Steps;

internal class MigrateCalculationsStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId,
    CalculationsToMigrate calculationsToMigrate)
        : StepExecutor(context, defaultRetryOptions, instanceId)
{
    internal const string StepName = "Migrer beregninger";
    internal const int StepSequence = 2;

    private readonly CalculationsToMigrate _calculationsToMigrate = calculationsToMigrate;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<OrchestrationStepTerminationState> OnExecuteAsync()
    {
        var calculationIdsToMigrate = _calculationsToMigrate.CalculationIdsToMigrate;

        // Fan-out/fan-in pattern: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-cloud-backup?tabs=csharp
        var migrateActivityTasks = new Task[calculationIdsToMigrate.Count];
        for (var i = 0; i < calculationIdsToMigrate.Count; i++)
        {
            var calculationToMigrateId = calculationIdsToMigrate.ElementAt(i);

            migrateActivityTasks[i] = Context.CallActivityAsync(
                name: nameof(MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1),
                input: new MigrateCalculationActivity_MigrateCalculationsFromWholesale_V1.ActivityInput(
                    CalculationToMigrateId: calculationToMigrateId),
                options: DefaultRetryOptions);
        }

        await Task.WhenAll(migrateActivityTasks);

        await Context.CallActivityAsync(
            name: nameof(ValidateMigratedCalculationsActivity_MigrateCalculationsFromWholesale_V1),
            options: DefaultRetryOptions);

        return OrchestrationStepTerminationState.Succeeded;
    }
}
