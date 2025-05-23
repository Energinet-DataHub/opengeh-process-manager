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

using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

public class StepInstanceLifecycle
{
    internal StepInstanceLifecycle(
        bool canBeSkipped)
    {
        State = StepInstanceLifecycleState.Pending;
        CanBeSkipped = canBeSkipped;
    }

    public StepInstanceLifecycleState State { get; private set; }

    public StepInstanceTerminationState? TerminationState { get; private set; }

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
    /// Specifies if the step supports beeing skipped.
    /// If <see langword="false"/> then the step cannot be transitioned
    /// to the Skipped state.
    /// </summary>
    public bool CanBeSkipped { get; }

    public void TransitionToRunning(IClock clock)
    {
        if (State is not StepInstanceLifecycleState.Pending)
            throw new InvalidOperationException($"Cannot change state from '{State}' to '{StepInstanceLifecycleState.Running}'.");

        State = StepInstanceLifecycleState.Running;
        StartedAt = clock.GetCurrentInstant();
    }

    public void TransitionToTerminated(IClock clock, StepInstanceTerminationState terminationState)
    {
        switch (terminationState)
        {
            case StepInstanceTerminationState.Succeeded:
            case StepInstanceTerminationState.Failed:
                if (State is not StepInstanceLifecycleState.Running)
                    throw new InvalidOperationException($"Cannot change termination state to '{terminationState}' when '{State}'.");
                break;

            case StepInstanceTerminationState.Skipped:
                if (State is not StepInstanceLifecycleState.Pending)
                    throw new InvalidOperationException($"Cannot change termination state to '{terminationState}' when '{State}'.");
                if (CanBeSkipped == false)
                    throw new InvalidOperationException($"Step cannot be skipped.");
                break;

            default:
                throw new InvalidOperationException($"Unsupported termination state '{terminationState}'.");
        }

        State = StepInstanceLifecycleState.Terminated;
        TerminationState = terminationState;
        TerminatedAt = clock.GetCurrentInstant();
    }
}
