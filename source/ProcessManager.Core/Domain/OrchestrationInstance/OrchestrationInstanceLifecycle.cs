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

using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

public class OrchestrationInstanceLifecycle
{
    internal OrchestrationInstanceLifecycle(OperatingIdentity createdBy, IClock clock, Instant? runAt)
    {
        CreatedBy = new OperatingIdentityComplexType(createdBy);
        CreatedAt = clock.GetCurrentInstant();
        ScheduledToRunAt = runAt;

        State = OrchestrationInstanceLifecycleState.Pending;
    }

    /// <summary>
    /// Used by Entity Framework
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // ReSharper disable once UnusedMember.Local -- Used by Entity Framework
    private OrchestrationInstanceLifecycle()
    {
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public OrchestrationInstanceLifecycleState State { get; private set; }

    public OrchestrationInstanceTerminationState? TerminationState { get; private set; }

    /// <summary>
    /// The identity that caused this orchestration instance to be created.
    /// </summary>
    public OperatingIdentityComplexType CreatedBy { get; }

    /// <summary>
    /// The time when the orchestration instance was created (State => Pending).
    /// </summary>
    public Instant CreatedAt { get; }

    /// <summary>
    /// The time when the orchestration instance should be executed by the Scheduler.
    /// </summary>
    public Instant? ScheduledToRunAt { get; }

    /// <summary>
    /// The time when the Process Manager has queued the orchestration instance
    /// for execution by Durable Functions (State => Queued).
    /// </summary>
    public Instant? QueuedAt { get; private set; }

    /// <summary>
    /// The time when the Process Manager was used from Durable Functions to
    /// transition the state to Running.
    /// </summary>
    public Instant? StartedAt { get; private set; }

    /// <summary>
    /// The time when the Process Manager was used from Durable Functions to
    /// transition the state to Terminated.
    /// </summary>
    public Instant? TerminatedAt { get; private set; }

    /// <summary>
    /// The identity that caused this orchestration instance to be canceled.
    /// </summary>
    public OperatingIdentityComplexType? CanceledBy { get; private set; }

    internal string? CreatedByIdentityType { get; private set; }

    internal Guid? CreatedByUserId { get; private set; }

    public bool IsPendingForScheduledStart()
    {
        return
            State == OrchestrationInstanceLifecycleState.Pending
            && ScheduledToRunAt.HasValue;
    }

    public void TransitionToQueued(IClock clock)
    {
        if (State is not OrchestrationInstanceLifecycleState.Pending)
            ThrowInvalidStateTransitionException(State, OrchestrationInstanceLifecycleState.Queued);

        State = OrchestrationInstanceLifecycleState.Queued;
        QueuedAt = clock.GetCurrentInstant();
    }

    public void TransitionToRunning(IClock clock)
    {
        if (State is not OrchestrationInstanceLifecycleState.Queued)
            ThrowInvalidStateTransitionException(State, OrchestrationInstanceLifecycleState.Running);

        State = OrchestrationInstanceLifecycleState.Running;
        StartedAt = clock.GetCurrentInstant();
    }

    public void TransitionToSucceeded(IClock clock)
    {
        TransitionToTerminated(clock, OrchestrationInstanceTerminationState.Succeeded);
    }

    public void TransitionToFailed(IClock clock)
    {
        TransitionToTerminated(clock, OrchestrationInstanceTerminationState.Failed);
    }

    public void TransitionToUserCanceled(IClock clock, UserIdentity userIdentity)
    {
        TransitionToTerminated(clock, OrchestrationInstanceTerminationState.UserCanceled, userIdentity);
    }

    private void TransitionToTerminated(IClock clock, OrchestrationInstanceTerminationState terminationState, UserIdentity? userIdentity = default)
    {
        switch (terminationState)
        {
            case OrchestrationInstanceTerminationState.Succeeded:
            case OrchestrationInstanceTerminationState.Failed:
                if (State is not OrchestrationInstanceLifecycleState.Running)
                    throw new InvalidOperationException($"Cannot change termination state to '{terminationState}' when '{State}'.");
                break;

            case OrchestrationInstanceTerminationState.UserCanceled:
                if (!IsPendingForScheduledStart())
                    throw new InvalidOperationException("User cannot cancel orchestration instance.");
                if (userIdentity == null)
                    throw new InvalidOperationException("User identity must be specified.");
                CanceledBy = new OperatingIdentityComplexType(userIdentity);
                break;

            default:
                throw new InvalidOperationException($"Unsupported termination state '{terminationState}'.");
        }

        State = OrchestrationInstanceLifecycleState.Terminated;
        TerminationState = terminationState;
        TerminatedAt = clock.GetCurrentInstant();
    }

    private void ThrowInvalidStateTransitionException(
        OrchestrationInstanceLifecycleState currentState,
        OrchestrationInstanceLifecycleState desiredState)
    {
        throw new InvalidOperationException($"Cannot change state from '{State}' to '{desiredState}'.");
    }
}
