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
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Shared.Processes.Activities;

#pragma warning disable CA2007
public abstract class StepExecutor(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId)
{
    protected TaskOrchestrationContext Context { get; } = context;

    protected TaskOptions DefaultRetryOptions { get; } = new(defaultRetryOptions);

    protected OrchestrationInstanceId InstanceId { get; } = instanceId;

    protected abstract int StepSequenceNumber { get; }

    public async Task ExecuteStepAsync()
    {
        await Context.CallActivityAsync(
            name: nameof(TransitionStepToRunningActivity_V1),
            input: new TransitionStepToRunningActivity_V1.ActivityInput(
                OrchestrationInstanceId: InstanceId,
                StepSequence: StepSequenceNumber),
            options: DefaultRetryOptions);

        OrchestrationStepTerminationState stepTerminationState;
        try
        {
            stepTerminationState = await PerformStepAsync();
        }
        catch (Exception e)
        {
            var logger = Context.CreateReplaySafeLogger(GetType());
            logger.Log(
                logLevel: LogLevel.Error,
                exception: e,
                message: "Exception while performing step (InstanceId={OrchestrationInstanceId}).",
                InstanceId.Value);

            await Context.CallActivityAsync(
                name: nameof(TransitionOrchestrationAndStepToFailedActivity_V1),
                input: new TransitionOrchestrationAndStepToFailedActivity_V1.ActivityInput(
                    OrchestrationInstanceId: InstanceId,
                    FailedStepSequence: StepSequenceNumber,
                    FailedStepErrorMessage: e.ToString()),
                options: DefaultRetryOptions);

            throw;
        }

        await Context.CallActivityAsync(
            name: nameof(TransitionStepToTerminatedActivity_V1),
            input: new TransitionStepToTerminatedActivity_V1.ActivityInput(
                OrchestrationInstanceId: InstanceId,
                StepSequence: StepSequenceNumber,
                TerminationState: stepTerminationState),
            options: DefaultRetryOptions);
    }

    protected abstract Task<OrchestrationStepTerminationState> PerformStepAsync();
}

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
public abstract class StepExecutor<TStepOutput>(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId)
        : StepExecutor(context, defaultRetryOptions, instanceId)
{
    public new async Task<TStepOutput> ExecuteStepAsync()
    {
        await Context.CallActivityAsync(
            name: nameof(TransitionStepToRunningActivity_V1),
            input: new TransitionStepToRunningActivity_V1.ActivityInput(
                OrchestrationInstanceId: InstanceId,
                StepSequence: StepSequenceNumber),
            options: DefaultRetryOptions);

        OrchestrationStepTerminationState stepTerminationState;
        TStepOutput stepOutput;
        try
        {
            var stepResult = await PerformStepWithOutputAsync();
            stepTerminationState = stepResult.StepTerminationState;
            stepOutput = stepResult.StepOutput;
        }
        catch (Exception e)
        {
            var logger = Context.CreateReplaySafeLogger(GetType());
            logger.Log(
                logLevel: LogLevel.Error,
                exception: e,
                message: "Exception while performing step (InstanceId={OrchestrationInstanceId}).",
                InstanceId.Value);

            await Context.CallActivityAsync(
                name: nameof(TransitionOrchestrationAndStepToFailedActivity_V1),
                input: new TransitionOrchestrationAndStepToFailedActivity_V1.ActivityInput(
                    OrchestrationInstanceId: InstanceId,
                    FailedStepSequence: StepSequenceNumber,
                    FailedStepErrorMessage: e.ToString()),
                options: DefaultRetryOptions);

            throw;
        }

        await Context.CallActivityAsync(
            name: nameof(TransitionStepToTerminatedActivity_V1),
            input: new TransitionStepToTerminatedActivity_V1.ActivityInput(
                OrchestrationInstanceId: InstanceId,
                StepSequence: StepSequenceNumber,
                TerminationState: stepTerminationState),
            options: DefaultRetryOptions);

        return stepOutput;
    }

    [Obsolete("This method should not be called or implemented, use PerformStepWithOutputAsync() instead")]
    protected override Task<OrchestrationStepTerminationState> PerformStepAsync()
    {
        throw new NotSupportedException("This method should not be called, use PerformStepWithOutputAsync() instead");
    }

    protected abstract Task<(OrchestrationStepTerminationState StepTerminationState, TStepOutput StepOutput)>
        PerformStepWithOutputAsync();
}
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
#pragma warning restore CA2007
