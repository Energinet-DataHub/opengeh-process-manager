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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Steps;

internal class WaitForNotifyEventStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId,
    TimeSpan exampleNotifyEventTimeout)
        : StepExecutor<bool>(context, defaultRetryOptions, instanceId)
{
    internal const string StepDescription = "Wait for notify event";
    internal const int StepSequence = 1;

    private readonly TimeSpan _exampleNotifyEventTimeout = exampleNotifyEventTimeout;

    protected override int StepSequenceNumber => StepSequence;

    protected override async Task<StepOutput> OnExecuteAsync()
    {
        // Wait for notify event
        // If the event isn't received before the timeout, an exception will be thrown, and the StepExecutor<> will fail the step and orchestration.
        // We can handle it manually by using try/catch here instead.
        ExampleNotifyEventDataV1? notifyData = null;
        try
        {
            // Wait for the notify event.
            notifyData = await Context.WaitForExternalEvent<ExampleNotifyEventDataV1>(
                eventName: NotifyOrchestrationInstanceExampleNotifyEventV1.OrchestrationInstanceEventName,
                timeout: _exampleNotifyEventTimeout);
        }
        catch (TaskCanceledException)
        {
            var logger = Context.CreateReplaySafeLogger<Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1>();
            logger.Log(
                LogLevel.Error,
                "Timeout while waiting for example notify event (InstanceId={OrchestrationInstanceId}, Timeout={Timeout}).",
                InstanceId.Value,
                _exampleNotifyEventTimeout.ToString("g"));

            // Does not rethrow the exception, since we don't want the orchestration instance to fail.
        }

        if (notifyData != null)
        {
            // Set custom state of the step instance, so we can assert it in tests.
            await Context.CallActivityAsync(
                nameof(SetStepCustomStateActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1),
                new SetStepCustomStateActivity_Brs_X02_NotifyOrchestrationInstanceExample_V1.ActivityInput(
                    InstanceId,
                    StepSequenceNumber,
                    notifyData.Message),
                DefaultRetryOptions);
        }

        var hasReceivedExampleNotifyEvent = notifyData != null;
        var stepTerminationState = hasReceivedExampleNotifyEvent
            ? OrchestrationStepTerminationState.Succeeded
            : OrchestrationStepTerminationState.Failed;

        return new StepOutput(stepTerminationState, hasReceivedExampleNotifyEvent);
    }

    public record CustomState(
        string Message);
}
