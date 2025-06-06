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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Orchestration.Steps;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Orchestration;

internal class Orchestration_Brs_023_027_V1
{
    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_023_027.V1;

    private readonly TaskRetryOptions _defaultRetryOptions;

    private readonly TaskOptions _defaultTaskOptions;

    public Orchestration_Brs_023_027_V1()
    {
        // 30 seconds interval, backoff coefficient 2.0, 7 retries (initial attempt is included in the maxNumberOfAttempts)
        // 30 seconds * (2^7-1) = 3810 seconds = 63,5 minutes to use all retries
        _defaultRetryOptions = TaskRetryOptions.FromRetryPolicy(
            new RetryPolicy(
                maxNumberOfAttempts: 8,
                firstRetryInterval: TimeSpan.FromSeconds(30),
                backoffCoefficient: 2.0));

        _defaultTaskOptions = new TaskOptions(_defaultRetryOptions);
    }

    [Function(nameof(Orchestration_Brs_023_027_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var orchestrationInstanceContext = await InitializeOrchestrationAsync(context);

        await new CalculationStep(
                context,
                _defaultRetryOptions,
                orchestrationInstanceContext)
            .ExecuteAsync();

        // Step: Enqueue messages
        if (!orchestrationInstanceContext.SkippedStepsBySequence.Contains(EnqueueActorMessagesStep.EnqueueActorMessagesStepSequence))
        {
            await new EnqueueActorMessagesStep(
                context,
                _defaultRetryOptions,
                orchestrationInstanceContext)
                .ExecuteAsync();

            var integrationEventIdempotencyKey = context.NewGuid();

            // Messages have been prepared for the actors, we need to inform the other subsystems
            await context.CallActivityAsync(
                    nameof(PublishCalculationEnqueueCompletedActivity_brs_023_027_V1),
                    new PublishCalculationEnqueueCompletedActivity_brs_023_027_V1.ActivityInput(
                        orchestrationInstanceContext.OrchestrationInstanceId,
                        orchestrationInstanceContext.CalculationId,
                        integrationEventIdempotencyKey),
                    _defaultTaskOptions);
        }

        return await SetTerminateOrchestrationAsync(
            context,
            orchestrationInstanceContext.OrchestrationInstanceId,
            success: true);
    }

    private async Task<OrchestrationInstanceContext> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToRunningActivity_V1),
            new TransitionOrchestrationToRunningActivity_V1.ActivityInput(
                instanceId),
            _defaultTaskOptions);

        var executionContext = await context.CallActivityAsync<OrchestrationInstanceContext>(
            nameof(GetOrchestrationInstanceContextActivity_Brs_023_027_V1),
            new GetOrchestrationInstanceContextActivity_Brs_023_027_V1.ActivityInput(
                instanceId),
            _defaultTaskOptions);

        return executionContext;
    }

    private async Task<string> SetTerminateOrchestrationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        bool success)
    {
        var orchestrationTerminationState = success
            ? OrchestrationInstanceTerminationState.Succeeded
            : OrchestrationInstanceTerminationState.Failed;

        await context.CallActivityAsync(
            nameof(TransitionOrchestrationToTerminatedActivity_V1),
            new TransitionOrchestrationToTerminatedActivity_V1.ActivityInput(
                instanceId,
                orchestrationTerminationState),
            _defaultTaskOptions);

        return "Success";
    }
}
