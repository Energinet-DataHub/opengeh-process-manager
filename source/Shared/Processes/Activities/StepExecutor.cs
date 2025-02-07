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

/// <summary>
/// Base type for executing a step in an orchestration, which handles the step lifecycle transitions, including
/// failing the step instance and orchestration instance if an exception occurs.
/// <remarks>
/// !!! IMPORTANT !!!: This type (and types that inherit from it) is used directly in a durable function orchestration,
/// so all constraints regarding code in durable function also applies to this class, and the steps that inherit from it.
/// These constraints include:
/// <list type="bullet">
/// <item>
/// All awaited tasks must origin from the <see cref="TaskOrchestrationContext"/>. This includes not calling .ConfigureAwait()
/// since that will create a task that is not awaitable in a durable function.
/// </item>
/// <item>
/// All code must be replay safe.
/// </item>
/// </list>
/// </remarks>
/// </summary>
internal abstract class StepExecutorBase(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId)
{
    protected TaskOrchestrationContext Context { get; } = context;

    protected TaskOptions DefaultRetryOptions { get; } = new(defaultRetryOptions);

    protected OrchestrationInstanceId InstanceId { get; } = instanceId;

    protected abstract int StepSequenceNumber { get; }

    /// <summary>
    /// Transition the step instance's lifecycle to running.
    /// </summary>
    protected async Task TransitionStepToRunning()
    {
        await Context.CallActivityAsync(
            name: nameof(TransitionStepToRunningActivity_V1),
            input: new TransitionStepToRunningActivity_V1.ActivityInput(
                OrchestrationInstanceId: InstanceId,
                StepSequence: StepSequenceNumber),
            options: DefaultRetryOptions);
    }

    /// <summary>
    /// Transition the orchestration and step instance's lifecycle to failed.
    /// <remarks>Also logs the exception as an error.</remarks>
    /// </summary>
    /// <param name="e">The exception that caused the orchestration to fail.</param>
    protected async Task TransitionStepAndOrchestrationToFailed(Exception e)
    {
        var logger = Context.CreateReplaySafeLogger(GetType());
        logger.LogError(
            exception: e,
            message: "Unhandled exception while performing step (InstanceId={OrchestrationInstanceId}, StepFullName={StepFullName}, StepSequence={StepSequence}).",
            GetType().FullName,
            InstanceId.Value,
            StepSequenceNumber);

        await Context.CallActivityAsync(
            name: nameof(TransitionOrchestrationAndStepToFailedActivity_V1),
            input: new TransitionOrchestrationAndStepToFailedActivity_V1.ActivityInput(
                OrchestrationInstanceId: InstanceId,
                FailedStepSequence: StepSequenceNumber,
                FailedStepErrorMessage: e.ToString()),
            options: DefaultRetryOptions);
    }

    /// <summary>
    /// Transition the step instance's lifecycle to the given <paramref name="stepTerminationState"/>.
    /// </summary>
    /// <param name="stepTerminationState">The termination state to transition of the step.</param>
    protected async Task TransitionStepToTerminated(OrchestrationStepTerminationState stepTerminationState)
    {
        await Context.CallActivityAsync(
            name: nameof(TransitionStepToTerminatedActivity_V1),
            input: new TransitionStepToTerminatedActivity_V1.ActivityInput(
                OrchestrationInstanceId: InstanceId,
                StepSequence: StepSequenceNumber,
                TerminationState: stepTerminationState),
            options: DefaultRetryOptions);
    }
}

/// <inheritdoc/>
internal abstract class StepExecutor(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId)
        : StepExecutorBase(context, defaultRetryOptions, instanceId)
{
    /// <summary>
    /// Execute the step, handling the lifecycle transitions of the step instance, including failing the step and
    /// orchestration instances if an unhandled exception occurs in <see cref="PerformStepAsync"/>.
    /// </summary>
    /// <exception cref="Exception">
    /// If an unhandled exception happens in <see cref="PerformStepAsync"/>, the step and orchestration instance will
    /// be transitioned to failed, and the exception will be re-thrown.
    /// </exception>
    public async Task ExecuteStepAsync()
    {
        await TransitionStepToRunning();

        OrchestrationStepTerminationState stepTerminationState;
        try
        {
            stepTerminationState = await PerformStepAsync();
        }
        catch (Exception e)
        {
            await TransitionStepAndOrchestrationToFailed(e);
            throw;
        }

        await TransitionStepToTerminated(stepTerminationState);
    }

    /// <summary>
    /// Perform the actual step logic. If an unhandled exception occurs, the step and orchestration instance will be transitioned
    /// to failed.
    /// <remarks>
    /// !!! IMPORTANT !!!: The implementation of this method is used directly in a durable function orchestration,
    /// so all constraints regarding code in durable function also applies to the method implementation,
    /// in the types that inherit from it.
    /// These constraints include:
    /// <list type="bullet">
    /// <item>
    /// All awaited tasks must origin from the <see cref="TaskOrchestrationContext"/>. This includes not calling .ConfigureAwait()
    /// since that will create a task that is not awaitable in a durable function.
    /// </item>
    /// <item>
    /// All code must be replay safe.
    /// </item>
    /// </list>
    /// </remarks>
    /// </summary>
    /// <returns>The step termination state, which the step instance will be transitioned to.</returns>
    protected abstract Task<OrchestrationStepTerminationState> PerformStepAsync();
}

/// <inheritdoc/>
internal abstract class StepExecutor<TStepOutput>(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceId instanceId)
        : StepExecutorBase(context, defaultRetryOptions, instanceId)
{
    /// <summary>
    /// Execute the step, handling the lifecycle transitions of the step instance, including failing the step and
    /// orchestration instances if an unhandled exception occurs in <see cref="PerformStepAsync"/>.
    /// </summary>
    /// <exception cref="Exception">
    /// If an unhandled exception happens in <see cref="PerformStepAsync"/>, the step and orchestration instance will
    /// be transitioned to failed, and the exception will be re-thrown.
    /// </exception>
    public async Task<TStepOutput> ExecuteStepAsync()
    {
        await TransitionStepToRunning();

        OrchestrationStepTerminationState stepTerminationState;
        TStepOutput stepOutput;
        try
        {
            var stepResult = await PerformStepAsync();
            stepTerminationState = stepResult.TerminationState;
            stepOutput = stepResult.Output;
        }
        catch (Exception e)
        {
            await TransitionStepAndOrchestrationToFailed(e);
            throw;
        }

        await TransitionStepToTerminated(stepTerminationState);

        return stepOutput;
    }

    /// <summary>
    /// Perform the actual step logic. If an unhandled exception occurs, the step and orchestration instance will be transitioned
    /// to failed.
    /// <remarks>
    /// !!! IMPORTANT !!!: The implementation of this method is used directly in a durable function orchestration,
    /// so all constraints regarding code in durable function also applies to the method implementation,
    /// in the types that inherit from it.
    /// These constraints include:
    /// <list type="bullet">
    /// <item>
    /// All awaited tasks must origin from the <see cref="TaskOrchestrationContext"/>. This includes not calling .ConfigureAwait()
    /// since that will create a task that is not awaitable in a durable function.
    /// </item>
    /// <item>
    /// All code must be replay safe.
    /// </item>
    /// </list>
    /// </remarks>
    /// </summary>
    /// <returns>The step output, defined by the <typeparamref name="TStepOutput"/> type parameter.</returns>
    protected abstract Task<StepOutput> PerformStepAsync();

    internal record StepOutput(
        OrchestrationStepTerminationState TerminationState,
        TStepOutput Output);
}
#pragma warning restore CA2007
