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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1.Steps;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1;

internal class Orchestration_Brs_X03_V1
{
    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_X03.V1;

    private readonly TaskRetryOptions _defaultRetryOptions;

    public Orchestration_Brs_X03_V1()
    {
        _defaultRetryOptions = TaskRetryOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }

    [Function(nameof(Orchestration_Brs_X03_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Initialize orchestration instance
        var instanceId = await InitializeOrchestrationAsync(context);

        var businessValidationResult = await new BusinessValidationStep(context, _defaultRetryOptions, instanceId)
            .ExecuteStepAsync();
        await new EnqueueActorMessagesStep(
                context,
                _defaultRetryOptions,
                instanceId,
                businessValidationResult.ValidationErrors)
            .ExecuteStepAsync();

        // Terminate orchestration instance
        return await TerminateOrchestrationAsync(
            context,
            instanceId,
            businessValidationResult.ValidationErrors);
    }

    private async Task<OrchestrationInstanceId> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(
                instanceId),
            new TaskOptions(_defaultRetryOptions));
        await Task.CompletedTask;

        return instanceId;
    }

    private async Task<string> TerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        IReadOnlyCollection<ValidationError> validationErrors)
    {
        var validationSuccessful = validationErrors.Count == 0;
        var terminationState = validationSuccessful
            ? OrchestrationInstanceTerminationState.Succeeded
            : OrchestrationInstanceTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                instanceId,
                terminationState),
            new TaskOptions(_defaultRetryOptions));
        await Task.CompletedTask;

        return validationSuccessful
            ? "Success"
            : "Failed business validation";
    }
}
