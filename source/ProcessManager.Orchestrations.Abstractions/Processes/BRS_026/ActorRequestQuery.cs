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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026;

/// <summary>
/// Query for searching for BRS-026 or BRS-028 orchestration instances.
/// Must be JSON serializable.
/// </summary>
public sealed record ActorRequestQuery
    : SearchOrchestrationInstancesByCustomQuery<ActorRequestQueryResult>
{
    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    /// <param name="orchestrationDescriptionNames">The names of the orchestration descriptions to filter by.</param>
    /// <param name="activatedAtOrLater">The time (or later) when the orchestration instances was queued or scheduled to run at.</param>
    /// <param name="activatedAtOrEarlier">The time (or earlier) when the orchestration instances was queued or scheduled to run at.</param>
    public ActorRequestQuery(
        UserIdentityDto operatingIdentity,
        IReadOnlyCollection<string> orchestrationDescriptionNames,
        DateTimeOffset activatedAtOrLater,
        DateTimeOffset activatedAtOrEarlier)
            : base(operatingIdentity, "brs_026_028")
    {
        OrchestrationDescriptionNames = orchestrationDescriptionNames;
        ActivatedAtOrLater = activatedAtOrLater;
        ActivatedAtOrEarlier = activatedAtOrEarlier;
    }

    /// <summary>
    /// The names of the orchestration descriptions to filter by.
    /// </summary>
    public IReadOnlyCollection<string> OrchestrationDescriptionNames { get; }

    /// <summary>
    /// The time (or later) when the orchestration instances was queued or scheduled to run at.
    /// </summary>
    public DateTimeOffset ActivatedAtOrLater { get; }

    /// <summary>
    /// The time (or earlier) when the orchestration instances was queued or scheduled to run at.
    /// </summary>
    public DateTimeOffset ActivatedAtOrEarlier { get; }
}
