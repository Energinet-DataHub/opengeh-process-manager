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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.InternalProcesses.MigrateCalculationsFromWholesale;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1.Steps;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.V1;

internal class Orchestration_MigrateCalculationsFromWholesale_V1
{
    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = MigrateCalculationsFromWholesaleUniqueName.V1;

    private readonly TaskRetryOptions _defaultRetryOptions;

    public Orchestration_MigrateCalculationsFromWholesale_V1()
    {
        // 30 seconds interval, backoff coefficient 2.0, 7 retries (initial attempt is included in the maxNumberOfAttempts)
        // 30 seconds * (2^7-1) = 3810 seconds = 63,5 minutes to use all retries
        _defaultRetryOptions = TaskRetryOptions.FromRetryPolicy(
            new RetryPolicy(
                maxNumberOfAttempts: 8,
                firstRetryInterval: TimeSpan.FromSeconds(30),
                backoffCoefficient: 2.0));
    }

    [Function(nameof(Orchestration_MigrateCalculationsFromWholesale_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var instanceId = await InitializeOrchestrationAsync(context);

        var calculationsToMigrate = await new GetCalculationsToMigrateStep(
            context,
            _defaultRetryOptions,
            instanceId).ExecuteAsync();

        await new MigrateCalculationsStep(
            context,
            _defaultRetryOptions,
            instanceId,
            calculationsToMigrate).ExecuteAsync();

        return await TerminateOrchestrationAsync(
            context: context,
            instanceId: instanceId);
    }

    private async Task<OrchestrationInstanceId> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(
                instanceId),
            new TaskOptions(_defaultRetryOptions));

        return instanceId;
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId)
    {
        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                instanceId,
                OrchestrationInstanceTerminationState.Succeeded),
            new TaskOptions(_defaultRetryOptions));

        return "Success";
    }
}
