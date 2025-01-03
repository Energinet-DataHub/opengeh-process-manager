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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

/// <summary>
/// Represents the instance of an orchestration.
/// It contains state information about the instance, and is linked
/// to the orchestration description that it is an instance of.
/// </summary>
public class OrchestrationInstance
{
    private readonly List<StepInstance> _steps;

    private OrchestrationInstance(
        OrchestrationDescriptionId orchestrationDescriptionId,
        OperatingIdentity identity,
        IClock clock,
        Instant? runAt = default)
    {
        Id = new OrchestrationInstanceId(Guid.NewGuid());
        Lifecycle = new OrchestrationInstanceLifecycleState(identity, clock, runAt);
        ParameterValue = new();
        CustomState = new OrchestrationInstanceCustomState(string.Empty);

        _steps = [];
        Steps = _steps.AsReadOnly();

        OrchestrationDescriptionId = orchestrationDescriptionId;
    }

    /// <summary>
    /// Used by Entity Framework
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // ReSharper disable once UnusedMember.Local -- Used by Entity Framework
    private OrchestrationInstance()
    {
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public OrchestrationInstanceId Id { get; }

    /// <summary>
    /// The high-level lifecycle states that all orchestration instances can go through.
    /// </summary>
    public OrchestrationInstanceLifecycleState Lifecycle { get; }

    /// <summary>
    /// Contains the Durable Functions orchestration input parameter value.
    /// </summary>
    public ParameterValue ParameterValue { get; }

    /// <summary>
    /// Steps the orchestration instance is going through, and which should be
    /// visible to the users (e.g. shown in the UI).
    /// </summary>
    public IReadOnlyCollection<StepInstance> Steps { get; }

    /// <summary>
    /// Any custom state of the orchestration instance.
    /// </summary>
    public OrchestrationInstanceCustomState CustomState { get; }

    /// <summary>
    /// The orchestration description for the Durable Functions orchestration which describes
    /// the workflow that the orchestration instance is an instance of.
    /// </summary>
    internal OrchestrationDescriptionId OrchestrationDescriptionId { get; }

    /// <summary>
    /// Transition a step's lifecycle to running
    /// </summary>
    /// <param name="sequence">The sequence number of the step to transition</param>
    /// <param name="clock"></param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if a step with the given <paramref name="sequence"/> isn't found.</exception>
    public void TransitionStepToRunning(int sequence, IClock clock)
    {
        var step = Steps.SingleOrDefault(s => s.Sequence == sequence);

        if (step == null)
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "A step with the given sequence does not exist");

        step.Lifecycle.TransitionToRunning(clock);
    }

    /// <summary>
    /// Transition a step's lifecycle to terminated, with the given <paramref name="terminationState"/>
    /// </summary>
    /// <param name="sequence">The sequence number of the step to transition</param>
    /// <param name="terminationState">The state of the termination step (Succeeded, failed etc.)</param>
    /// <param name="clock"></param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if a step with the given <paramref name="sequence"/> isn't found.</exception>
    public void TransitionStepToTerminated(int sequence, OrchestrationStepTerminationStates terminationState, IClock clock)
    {
        var step = Steps.SingleOrDefault(s => s.Sequence == sequence);

        if (step == null)
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "A step with the given sequence does not exist");

        step.Lifecycle.TransitionToTerminated(clock, terminationState);
    }

    /// <summary>
    /// Factory method that ensures domain rules are obeyed when creating a new
    /// orchestration instance.
    /// </summary>
    internal static OrchestrationInstance CreateFromDescription(
        OperatingIdentity identity,
        OrchestrationDescription.OrchestrationDescription description,
        IReadOnlyCollection<int> skipStepsBySequence,
        IClock clock,
        Instant? runAt = default)
    {
        foreach (var stepSequence in skipStepsBySequence)
        {
            var stepOrDefault = description.Steps.FirstOrDefault(step => step.Sequence == stepSequence);
            if (stepOrDefault == null)
                throw new InvalidOperationException($"No step description matches the sequence '{stepSequence}'.");

            if (stepOrDefault.CanBeSkipped == false)
                throw new InvalidOperationException($"Step description with sequence '{stepSequence}' cannot be skipped.");
        }

        if (runAt.HasValue && description.CanBeScheduled == false)
            throw new InvalidOperationException("Orchestration description cannot be scheduled.");

        var orchestrationInstance = new OrchestrationInstance(
            description.Id,
            identity,
            clock,
            runAt);

        foreach (var stepDefinition in description.Steps)
        {
            var stepInstance = new StepInstance(
                orchestrationInstance.Id,
                stepDefinition.Description,
                stepDefinition.Sequence,
                stepDefinition.CanBeSkipped);

            if (skipStepsBySequence.Contains(stepInstance.Sequence))
            {
                stepInstance.Lifecycle.TransitionToTerminated(clock, OrchestrationStepTerminationStates.Skipped);
            }

            orchestrationInstance._steps.Add(stepInstance);
        }

        return orchestrationInstance;
    }
}
