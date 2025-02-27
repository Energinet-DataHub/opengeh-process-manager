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

using Energinet.DataHub.ProcessManager.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

/// <summary>
/// A custom designed class for the feature Migrate Wholesale Calculations,
/// so we avoid exposing inner details to Orchestrations, and can easily remove
/// it again, when its purpose has been fulfilled.
/// </summary>
internal sealed class OrchestrationInstanceFactory : IOrchestrationInstanceFactory, IClock
{
    private Instant CreatedAt { get; set; }

    /// <inheritdoc/>
    public OrchestrationInstance CreateQueuedOrchestrationInstance(
        OrchestrationDescription orchestrationDescription,
        Guid createdByUserId,
        Instant createdTime,
        Instant scheduledAt,
        IReadOnlyCollection<int> skipStepsBySequence)
    {
        CreatedAt = createdTime;

        var identity = new UserIdentity(
            new UserId(createdByUserId),
            DataHubSystemAdministrator.UserIdentity.Actor);

        // We use the class implementation of IClock to set "CreatedAt" when the orchestration instance is created.
        // Any skipped steps will use the same timestamp for "TerminatedAt".
        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            identity,
            orchestrationDescription,
            skipStepsBySequence,
            this,
            scheduledAt);

        // We use the same timestamp for "QueuedAt"
        orchestrationInstance.Lifecycle.TransitionToQueued(this);

        return orchestrationInstance;
    }

    Instant IClock.GetCurrentInstant()
    {
        return CreatedAt;
    }
}
