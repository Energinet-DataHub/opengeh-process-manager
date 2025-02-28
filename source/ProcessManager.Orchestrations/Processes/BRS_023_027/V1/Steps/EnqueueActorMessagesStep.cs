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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities.EnqueActorMessagesStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Steps;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "ConfigureAwait must not be used in durable function code")]
internal class EnqueueActorMessagesStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceContext orchestrationInstanceContext)
        : StepExecutor<bool>(
            context,
            defaultRetryOptions,
            orchestrationInstanceContext.OrchestrationInstanceId)
{
    internal const string StepDescription = "Udsend beskeder";
    internal const int EnqueueActorMessagesStepSequence = 2;

    protected override int StepSequenceNumber => EnqueueActorMessagesStepSequence;

    protected override async Task<StepOutput> OnExecuteAsync()
    {
        var idempotencyKey = Context.NewGuid();
        var timeout = TimeSpan.FromSeconds(
            orchestrationInstanceContext.OrchestrationOptions.MessagesEnqueuingExpiryTimeInSeconds);
        var calculationData = new CalculationEnqueueActorMessagesV1(
            CalculationId: orchestrationInstanceContext.CalculationId);

        await Context.CallActivityAsync(
            nameof(EnqueueActorMessagesActivity_Brs_023_027_V1),
            new EnqueueActorMessagesActivity_Brs_023_027_V1.ActivityInput(
                orchestrationInstanceContext.OrchestrationInstanceId,
                calculationData,
                idempotencyKey),
            DefaultRetryOptions);

        try
        {
            var enqueueEvent = await Context.WaitForExternalEvent<CalculationEnqueueActorMessagesCompletedNotifyEventV1>(
                eventName: CalculationEnqueueActorMessagesCompletedNotifyEventV1.EventName,
                timeout: timeout);

            if (enqueueEvent.Success)
                return new StepOutput(OrchestrationStepTerminationState.Succeeded, enqueueEvent.Success);

            throw new Exception($"Enqueue messages did not finish within {orchestrationInstanceContext.OrchestrationOptions.MessagesEnqueuingExpiryTimeInSeconds} seconds");
        }
        catch (TaskCanceledException)
        {
            var logger = Context.CreateReplaySafeLogger<Orchestration_Brs_023_027_V1>();
            logger.Log(
                LogLevel.Error,
                "Timeout while waiting for enqueue actor messages to complete (InstanceId={OrchestrationInstanceId}, Timeout={Timeout}).",
                orchestrationInstanceContext.OrchestrationInstanceId.Value,
                timeout.ToString("g"));
            throw;
        }
    }
}
