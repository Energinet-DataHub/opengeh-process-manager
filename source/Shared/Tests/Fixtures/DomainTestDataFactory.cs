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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

/// <summary>
/// Create types in the ProcessManager.Core.Domain namespace.
/// </summary>
public static class DomainTestDataFactory
{
    public static readonly IdentityTuple EnergySupplier;

    public static readonly IdentityTuple BalanceResponsibleParty;

    static DomainTestDataFactory()
    {
        EnergySupplier = new(
            "1234567890222",
            ActorRole.EnergySupplier,
            "6FF394EF-4B0D-4EBE-8477-522544C34E84");

        BalanceResponsibleParty = new(
            "1234567890333",
            ActorRole.BalanceResponsibleParty,
            "0CBB5427-E406-4E6F-A975-E21A69A3CA2A");
    }

    /// <summary>
    /// Create an Orchestration Description with the following properties:
    ///  - Is enabled by default (can be changed using <paramref name="isEnabled"/>)
    ///  - Can be scheduled
    ///  - Is used with Durable Functions (can be changed using <paramref name="isDurableFunction"/>)
    ///  - Isn't recurring (can be changed using <paramref name="recurringCronExpression"/>)
    ///  - Has input paramters of type <see cref="OrchestrationParameter"/>
    ///  - Has 3 steps, of which the last can be skipped
    /// </summary>
    public static OrchestrationDescription CreateOrchestrationDescription(
        OrchestrationDescriptionUniqueName? uniqueName = default,
        bool isDurableFunction = true,
        string? recurringCronExpression = default,
        bool isEnabled = true)
    {
        var orchestrationDescription = new OrchestrationDescription(
            uniqueName: uniqueName ?? new OrchestrationDescriptionUniqueName("TestOrchestration", 4),
            canBeScheduled: true,
            functionName: isDurableFunction ? "TestOrchestrationFunction" : string.Empty);

        if (recurringCronExpression != null)
            orchestrationDescription.RecurringCronExpression = recurringCronExpression;

        orchestrationDescription.ParameterDefinition.SetFromType<OrchestrationParameter>();

        orchestrationDescription.AppendStepDescription("Test step 1");
        orchestrationDescription.AppendStepDescription("Test step 2");
        orchestrationDescription.AppendStepDescription("Test step 3", canBeSkipped: true, skipReason: "Because we are testing");

        orchestrationDescription.IsEnabled = isEnabled;

        return orchestrationDescription;
    }

    /// <summary>
    /// Create an Orchestration Instance by a UserIdentity from an Orchestration Description that
    /// should be created similar to how it is done by
    /// <see cref="CreateOrchestrationDescription(OrchestrationDescriptionUniqueName?, bool, string?, bool)"/>.
    /// </summary>
    public static OrchestrationInstance CreateUserInitiatedOrchestrationInstance(
        OrchestrationDescription orchestrationDescription,
        UserIdentity? createdByUserIdentity = default,
        Instant? runAt = default,
        int? testInt = default)
    {
        var operatingIdentity = createdByUserIdentity ?? EnergySupplier.UserIdentity;

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            operatingIdentity,
            orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            runAt: runAt,
            idempotencyKey: null,
            actorMessageId: null,
            transactionId: null,
            meteringPointId: null);

        orchestrationInstance.ParameterValue.SetFromInstance(new OrchestrationParameter(
            TestString: "Test string",
            TestInt: testInt ?? 42));

        orchestrationInstance.CustomState.SetFromInstance(new OrchestrationInstanceCustomState(
            TestId: Guid.NewGuid(),
            TestString: "Something new"));

        return orchestrationInstance;
    }

    /// <summary>
    /// Create an Orchestration Instance by an ActorIdentity from an Orchestration Description that
    /// should be created similar to how it is done by
    /// <see cref="CreateOrchestrationDescription(OrchestrationDescriptionUniqueName?, bool, string?, bool)"/>.
    /// </summary>
    public static OrchestrationInstance CreateActorInitiatedOrchestrationInstance(
        OrchestrationDescription orchestrationDescription,
        ActorIdentity? createdByActorIdentity = default,
        IdempotencyKey? idempotencyKey = default)
    {
        var operatingIdentity = createdByActorIdentity ?? EnergySupplier.ActorIdentity;

        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            operatingIdentity,
            orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            runAt: null,
            idempotencyKey: idempotencyKey,
            actorMessageId: new ActorMessageId(Guid.NewGuid().ToString()),
            transactionId: new TransactionId(Guid.NewGuid().ToString()),
            meteringPointId: new MeteringPointId(Guid.NewGuid().ToString()));

        return orchestrationInstance;
    }

    /// <summary>
    /// Create orchestration instances from <paramref name="orchestrationDescription"/>
    /// in the following lifecycle states:
    ///  - Pending
    ///  - Queued
    ///  - Running
    ///  - Terminated as succeeded
    ///  - Terminated as failed
    ///
    /// If <paramref name="isRunningStartedAt"/> is specified, then this value
    /// is used when transitioning to Running.
    ///
    /// If <paramref name="isTerminatedAsSucceededAt"/> is specified, then this value
    /// is used when transitioning to terminated as succeeded.
    /// </summary>
    public static (
            OrchestrationInstance IsPending,
            OrchestrationInstance IsQueued,
            OrchestrationInstance IsRunning,
            OrchestrationInstance IsTerminatedAsSucceeded,
            OrchestrationInstance IsTerminatedAsFailed)
        CreateLifecycleDataset(
            OrchestrationDescription orchestrationDescription,
            Instant isRunningStartedAt = default,
            Instant isTerminatedAsSucceededAt = default)
    {
        var isPending = OrchestrationInstance.CreateFromDescription(
            identity: EnergySupplier.UserIdentity,
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);

        var isQueued = OrchestrationInstance.CreateFromDescription(
            identity: EnergySupplier.UserIdentity,
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
        isQueued.Lifecycle.TransitionToQueued(SystemClock.Instance);

        var isRunning = OrchestrationInstance.CreateFromDescription(
            identity: EnergySupplier.UserIdentity,
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
        isRunning.Lifecycle.TransitionToQueued(SystemClock.Instance);
        if (isRunningStartedAt == default)
        {
            isRunning.Lifecycle.TransitionToRunning(SystemClock.Instance);
        }
        else
        {
            var clockMock = new Mock<IClock>();
            clockMock.Setup(m => m.GetCurrentInstant())
                .Returns(isRunningStartedAt);
            isRunning.Lifecycle.TransitionToRunning(clockMock.Object);
        }

        var isTerminatedAsSucceeded = OrchestrationInstance.CreateFromDescription(
            identity: EnergySupplier.UserIdentity,
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
        isTerminatedAsSucceeded.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isTerminatedAsSucceeded.Lifecycle.TransitionToRunning(SystemClock.Instance);
        if (isTerminatedAsSucceededAt == default)
        {
            isTerminatedAsSucceeded.Lifecycle.TransitionToSucceeded(SystemClock.Instance);
        }
        else
        {
            var clockMock = new Mock<IClock>();
            clockMock.Setup(m => m.GetCurrentInstant())
                .Returns(isTerminatedAsSucceededAt);
            isTerminatedAsSucceeded.Lifecycle.TransitionToSucceeded(clockMock.Object);
        }

        var isTerminatedAsFailed = OrchestrationInstance.CreateFromDescription(
            identity: EnergySupplier.UserIdentity,
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);
        isTerminatedAsFailed.Lifecycle.TransitionToQueued(SystemClock.Instance);
        isTerminatedAsFailed.Lifecycle.TransitionToRunning(SystemClock.Instance);
        isTerminatedAsFailed.Lifecycle.TransitionToFailed(SystemClock.Instance);

        return (isPending, isQueued, isRunning, isTerminatedAsSucceeded, isTerminatedAsFailed);
    }

    public sealed record OrchestrationParameter(
        string? TestString,
        int? TestInt = default);

    public sealed record OrchestrationInstanceCustomState(
        Guid TestId,
        string? TestString);

    #region IdentityTuple

    public sealed record IdentityTuple
    {
        public IdentityTuple(
            string actorNumber,
            ActorRole actorRole,
            string userId)
        {
            ActorIdentity = new(
                new Actor(
                    ActorNumber.Create(actorNumber),
                    actorRole));

            UserIdentity = new(
                new UserId(Guid.Parse(userId)),
                ActorIdentity.Actor);
        }

        public UserIdentity UserIdentity { get; }

        public ActorIdentity ActorIdentity { get; }
    }

    #endregion
}
